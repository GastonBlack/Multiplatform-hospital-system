using HospitalPlatform.Api.Features.Staff.Models;
using HospitalPlatform.Api.Features.Staff.Seeds;
using HospitalPlatform.Api.Features.Users.Models;
using HospitalPlatform.Api.Features.Users.Seeds;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HospitalPlatform.Api.Infrastructure.Persistence.Seeds;

public static class DatabaseSeed
{
    public static async Task SeedAsync(HospitalDbContext db, IPasswordHasher<User> passwordHasher, string password)
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

        var user = UserSeed.ConfigureUser(db, passwordHasher, password);
        var employeeNumber = await UserSeed.ConfigureEmployeeNumberAsync(db, user.Id);
        StaffSeed.ConfigureAdministrator(db, user.Id, employeeNumber);

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
