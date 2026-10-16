using System.ComponentModel.DataAnnotations;
using System.Runtime;
using HospitalPlatform.Api.Features.Patients.DTOs;
using HospitalPlatform.Api.Features.Patients.Models;
using HospitalPlatform.Api.Features.Users.Models;
using HospitalPlatform.Api.Features.Users.Validation;
using HospitalPlatform.Api.Infrastructure.Middleware.Exceptions;
using HospitalPlatform.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HospitalPlatform.Api.Features.Patients.Services;

public class PatientRegistrationService : IPatientRegistrationService
{
    // //////////////////////////////////////////
    // Dependencies
    // //////////////////////////////////////////
    private readonly HospitalDbContext _db;
    private readonly IPasswordHasher<User> _passwordHasher;

    public PatientRegistrationService(HospitalDbContext db, IPasswordHasher<User> passwordHasher)
    {
        _db = db;
        _passwordHasher = passwordHasher;
    }

    // //////////////////////////////////////////
    // Registration
    // //////////////////////////////////////////
    public async Task<RegisterPatientResponse> RegisterAsync(RegisterPatientRequest request)
    {
        Validator.ValidateObject(request, new ValidationContext(request), validateAllProperties: true);
        EmailNormalizer.TryNormalize(request.Email, out var normalizedEmail);

        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = normalizedEmail,
            PasswordHash = string.Empty,
            ProfileType = ProfileType.Patient,
            AccountStatus = AccountStatus.PendingVerification,
            CreatedAt = now,
            UpdatedAt = now
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        var patient = new Patient
        {
            UserId = user.Id,
            NationalIdentificationNumber = request.NationalIdentificationNumber,
            PhoneNumber = request.PhoneNumber,
            DateOfBirth = request.DateOfBirth
        };

        await using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            _db.Users.Add(user);
            _db.Patients.Add(patient);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException exception)
        {
            var databaseError = exception.InnerException as PostgresException;

            if (databaseError == null || databaseError.SqlState != PostgresErrorCodes.UniqueViolation)
                throw;

            if (databaseError.ConstraintName == "IX_Users_Email")
                throw new ConflictException("Email is already registered.");

            if (databaseError.ConstraintName == "IX_Patients_NationalIdentificationNumber")
                throw new ConflictException("Identification number is already registered.");

            throw;
        }

        return new RegisterPatientResponse
        {
            UserId = user.Id,
            PatientId = patient.Id,
            AccountStatus = user.AccountStatus
        };
    }


    public async Task<GetPatientProfile> GetPatientProfileAsync(Guid userGuid)
    {
        var patientProfile = await(
            from patient in _db.Patients
            join user in _db.Users on patient.UserId equals user.Id
            where patient.UserId == userGuid
            select new GetPatientProfile
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                NationalIdentificationNumber = patient.NationalIdentificationNumber,
                PhoneNumber = patient.PhoneNumber,
                DateOfBirth = patient.DateOfBirth,
                AccountStatus = user.AccountStatus.ToString()
            }
        ).SingleOrDefaultAsync();

        return patientProfile ?? throw new NotFoundException("Patient not found.");
    }
}
