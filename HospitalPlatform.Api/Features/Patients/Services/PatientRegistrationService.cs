using System.ComponentModel.DataAnnotations;
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

public class PatientRegistrationService(HospitalDbContext dbContext, IPasswordHasher<User> passwordHasher)
    : IPatientRegistrationService
{
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
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        var patient = new Patient
        {
            UserId = user.Id,
            NationalIdentificationNumber = request.NationalIdentificationNumber,
            PhoneNumber = request.PhoneNumber,
            DateOfBirth = request.DateOfBirth
        };

        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        try
        {
            dbContext.Users.Add(user);
            dbContext.Patients.Add(patient);
            await dbContext.SaveChangesAsync();
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
}
