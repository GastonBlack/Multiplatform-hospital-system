using System.ComponentModel.DataAnnotations;
using HospitalPlatform.Api.Features.Staff.DTOs;
using HospitalPlatform.Api.Features.Users.Models;
using HospitalPlatform.Api.Features.Users.Services;
using HospitalPlatform.Api.Features.Users.Validation;
using HospitalPlatform.Api.Infrastructure.Middleware.Exceptions;
using HospitalPlatform.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HospitalPlatform.Api.Features.Staff.Services;

public class StaffRegistrationService : IStaffRegistrationService
{
    // //////////////////////////////////////////
    // Dependencies
    // //////////////////////////////////////////
    private readonly HospitalDbContext _db;
    private readonly IPasswordHasher<User> _passwordHasher;

    public StaffRegistrationService(HospitalDbContext db, IPasswordHasher<User> passwordHasher)
    {
        _db = db;
        _passwordHasher = passwordHasher;
    }

    // //////////////////////////////////////////
    // Registration
    // //////////////////////////////////////////
    public async Task<RegisterStaffResponse> RegisterAsync(RegisterStaffRequest request)
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
            ProfileType = ProfileType.Staff,
            AccountStatus = AccountStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        await using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var employeeNumber = await EmployeeNumberService.RegisterAsync(_db, user.Id);
            var staff = new Models.Staff
            {
                UserId = user.Id,
                EmployeeNumber = employeeNumber,
                StaffRole = request.StaffRole
            };
            _db.Users.Add(user);
            _db.Staff.Add(staff);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return new RegisterStaffResponse
            {
                UserId = user.Id,
                StaffId = staff.Id,
                EmployeeNumber = staff.EmployeeNumber,
                StaffRole = staff.StaffRole.ToString(),
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

            throw;
        }
    }
}
