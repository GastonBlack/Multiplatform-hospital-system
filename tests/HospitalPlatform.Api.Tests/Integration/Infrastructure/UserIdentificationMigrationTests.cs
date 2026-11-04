using System.Net;
using System.Net.Http.Json;
using HospitalPlatform.Api.Features.Authentication.DTOs;
using HospitalPlatform.Api.Features.Users.Models;
using HospitalPlatform.Api.Infrastructure.Persistence;
using HospitalPlatform.Api.Tests.Shared.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalPlatform.Api.Tests.Integration.Infrastructure;

public class UserIdentificationMigrationTests : IAsyncLifetime
{
    private readonly HospitalApiFactory factory = new();

    public async ValueTask InitializeAsync() => await factory.InitializeAsync();

    public async ValueTask DisposeAsync() => await factory.DisposeAsync();

    [Fact]
    public async Task Migrate_ExistingPatientAndStaff_PreservesAccountsAndMovesPatientIdentification()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261012224945_AuthenticationSessions");
        var patientUser = new User
        {
            FirstName = "Legacy", LastName = "Patient", Email = "legacy.patient@example.com", PasswordHash = string.Empty,
            ProfileType = ProfileType.Patient, AccountStatus = AccountStatus.PendingVerification
        };
        var passwordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>().HashPassword(patientUser, "Ab1!_?xy");
        var staffUserId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var timestamp = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Users" ("Id", "FirstName", "LastName", "Email", "PasswordHash", "ProfileType", "AccountStatus", "CreatedAt", "UpdatedAt")
                VALUES ({patientUser.Id}, 'Legacy', 'Patient', 'legacy.patient@example.com', {passwordHash}, 'Patient', 'PendingVerification', {timestamp}, {timestamp}),
                       ({staffUserId}, 'Legacy', 'Administrator', 'legacy.staff@example.com', 'unchanged-staff-hash', 'Staff', 'Active', {timestamp}, {timestamp});

                INSERT INTO "Patients" ("Id", "UserId", "ProfileType", "NationalIdentificationNumber", "PhoneNumber", "DateOfBirth")
                VALUES ({patientId}, {patientUser.Id}, 'Patient', '00123456', '+598 99 123 456', DATE '2000-01-01');

                INSERT INTO "EmployeeNumbers" ("EmployeeNumber", "UserId") VALUES ('EMP-000123', {staffUserId});
                INSERT INTO "Staff" ("Id", "UserId", "ProfileType", "EmployeeNumber", "StaffRole")
                VALUES ({staffId}, {staffUserId}, 'Staff', 'EMP-000123', 'Administrator');
                """);
            await transaction.CommitAsync();
        }

        await migrator.MigrateAsync();
        var migratedPatient = await db.Users.AsNoTracking().SingleAsync(user => user.Id == patientUser.Id);
        var migratedStaff = await db.Users.AsNoTracking().SingleAsync(user => user.Id == staffUserId);
        Assert.Equal("00123456", migratedPatient.NationalIdentificationNumber);
        Assert.Equal(passwordHash, migratedPatient.PasswordHash);
        Assert.Equal(timestamp, migratedPatient.CreatedAt);
        Assert.Equal(timestamp, migratedPatient.UpdatedAt);
        Assert.Null(migratedStaff.NationalIdentificationNumber);
        Assert.Equal("unchanged-staff-hash", migratedStaff.PasswordHash);
        Assert.Equal(patientId, (await db.Patients.SingleAsync()).Id);
        Assert.Equal(staffId, (await db.Staff.SingleAsync()).Id);
        Assert.Equal("EMP-000123", (await db.EmployeeNumbers.SingleAsync()).EmployeeNumber);

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            NationalIdentificationNumber = "00123456", Password = "Ab1!_?xy"
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        await migrator.MigrateAsync("20261012224945_AuthenticationSessions");
        var restored = await db.Database.SqlQueryRaw<string>(
            "SELECT \"NationalIdentificationNumber\" AS \"Value\" FROM \"Patients\"").SingleAsync();
        Assert.Equal("00123456", restored);
        await migrator.MigrateAsync();
        Assert.Equal("00123456", (await db.Users.AsNoTracking().SingleAsync(user => user.Id == patientUser.Id)).NationalIdentificationNumber);
        Assert.Equal(2, await db.Users.CountAsync());
    }
}
