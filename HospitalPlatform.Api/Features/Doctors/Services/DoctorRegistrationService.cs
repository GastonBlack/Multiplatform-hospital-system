using System.ComponentModel.DataAnnotations;
using HospitalPlatform.Api.Features.Doctors.DTOs;
using HospitalPlatform.Api.Features.Doctors.Models;
using HospitalPlatform.Api.Features.Users.Models;
using HospitalPlatform.Api.Features.Users.Services;
using HospitalPlatform.Api.Features.Users.Validation;
using HospitalPlatform.Api.Infrastructure.Middleware.Exceptions;
using HospitalPlatform.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HospitalPlatform.Api.Features.Doctors.Services;

public class DoctorRegistrationService : IDoctorRegistrationService
{
    // //////////////////////////////////////////
    // Dependencies
    // //////////////////////////////////////////
    private readonly HospitalDbContext _db;
    private readonly IPasswordHasher<User> _passwordHasher;

    public DoctorRegistrationService(HospitalDbContext db, IPasswordHasher<User> passwordHasher)
    {
        _db = db;
        _passwordHasher = passwordHasher;
    }

    // //////////////////////////////////////////
    // Registration
    // //////////////////////////////////////////
    public async Task<RegisterDoctorResponse> RegisterAsync(RegisterDoctorRequest request)
    {
        Validator.ValidateObject(request, new ValidationContext(request), validateAllProperties: true);
        EmailNormalizer.TryNormalize(request.Email, out var normalizedEmail);

        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = normalizedEmail,
            NationalIdentificationNumber = request.NationalIdentificationNumber,
            PasswordHash = string.Empty,
            ProfileType = ProfileType.Doctor,
            AccountStatus = AccountStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        await using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var employeeNumber = await EmployeeNumberService.RegisterAsync(_db, user.Id);
            var doctor = new Doctor
            {
                UserId = user.Id,
                EmployeeNumber = employeeNumber,
                MedicalLicenseNumber = request.MedicalLicenseNumber.Trim()
            };
            _db.Users.Add(user);
            _db.Doctors.Add(doctor);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return new RegisterDoctorResponse
            {
                UserId = user.Id,
                DoctorId = doctor.Id,
                EmployeeNumber = doctor.EmployeeNumber,
                MedicalLicenseNumber = doctor.MedicalLicenseNumber,
                AccountStatus = user.AccountStatus.ToString()
            };
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        } databaseError)
        {
            if (databaseError.ConstraintName == "IX_Users_Email")
                throw new ConflictException("Email is already registered.");

            if (databaseError.ConstraintName == "IX_Users_NationalIdentificationNumber")
                throw new ConflictException("Identification number is already registered.");

            if (databaseError.ConstraintName == "IX_Doctors_MedicalLicenseNumber")
                throw new ConflictException("Medical license number is already registered.");

            throw;
        }
    }
}
