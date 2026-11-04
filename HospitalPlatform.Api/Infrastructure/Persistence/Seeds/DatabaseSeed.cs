using HospitalPlatform.Api.Features.Staff.Models;
using HospitalPlatform.Api.Features.Staff.Seeds;
using HospitalPlatform.Api.Features.Users.Models;
using HospitalPlatform.Api.Features.Users.Seeds;
using HospitalPlatform.Api.Features.Users.Services;
using HospitalPlatform.Api.Features.Users.Validation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HospitalPlatform.Api.Infrastructure.Persistence.Seeds;

public static class DatabaseSeed
{
    public static async Task SeedAsync(HospitalDbContext db, IPasswordHasher<User> passwordHasher, string password, string nationalIdentificationNumber)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();

        // Serialize initialization, including a simultaneous staff registration.
        await db.Database.ExecuteSqlRawAsync("LOCK TABLE \"Staff\" IN SHARE ROW EXCLUSIVE MODE");
        if (await db.Staff.AnyAsync(staff => staff.StaffRole == StaffRole.Administrator))
        {
            await transaction.CommitAsync();
            return;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        if (password.Length is < 6 or > 12)
            throw new ArgumentException("Administrator password must be between 6 and 12 characters, as required by login.", nameof(password));

        ArgumentException.ThrowIfNullOrWhiteSpace(nationalIdentificationNumber);
        if (!new NationalIdentificationNumberAttribute().IsValid(nationalIdentificationNumber))
            throw new ArgumentException("Administrator identification number must contain exactly eight digits.", nameof(nationalIdentificationNumber));

        var user = UserSeed.ConfigureUser(db, passwordHasher, password, nationalIdentificationNumber);
        var employeeNumber = await EmployeeNumberService.RegisterAsync(db, user.Id);
        StaffSeed.ConfigureAdministrator(db, user.Id, employeeNumber);

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
