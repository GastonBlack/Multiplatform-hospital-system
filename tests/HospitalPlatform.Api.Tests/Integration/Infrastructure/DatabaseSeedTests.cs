using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HospitalPlatform.Api.Features.Authentication.DTOs;
using HospitalPlatform.Api.Features.Patients.DTOs;
using HospitalPlatform.Api.Features.Staff.Models;
using HospitalPlatform.Api.Features.Users.Models;
using HospitalPlatform.Api.Infrastructure.Persistence;
using HospitalPlatform.Api.Infrastructure.Persistence.Seeds;
using HospitalPlatform.Api.Tests.Shared.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalPlatform.Api.Tests.Integration.Infrastructure;

public class DatabaseSeedTests : IAsyncLifetime
{
    private readonly HospitalApiFactory factory = new();

    public async ValueTask InitializeAsync() => await factory.InitializeAsync();

    public async ValueTask DisposeAsync() => await factory.DisposeAsync();

    [Fact]
    public async Task Seed_ConcurrentAndRepeatedRuns_PreserveOneAdministratorAndItsCredentials()
    {
        await Task.WhenAll(SeedAsync("FirstSeed1!"), SeedAsync("SecondSeed1!"));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        var user = await db.Users.SingleAsync();
        var staff = await db.Staff.SingleAsync();
        var number = await db.EmployeeNumbers.SingleAsync();
        Assert.Equal("Default", user.FirstName);
        Assert.Equal("Administrator", user.LastName);
        Assert.Equal("administrator0101@gmail.com", user.Email);
        Assert.Equal("90000001", user.NationalIdentificationNumber);
        Assert.Equal(ProfileType.Staff, user.ProfileType);
        Assert.Equal(AccountStatus.Active, user.AccountStatus);
        Assert.Equal(user.Id, staff.UserId);
        Assert.Equal(user.Id, number.UserId);
        Assert.Equal(StaffRole.Administrator, staff.StaffRole);
        Assert.Equal("EMP-000001", staff.EmployeeNumber);
        Assert.Equal(staff.EmployeeNumber, number.EmployeeNumber);
        Assert.Equal(TimeSpan.Zero, user.CreatedAt.Offset);
        Assert.Equal(user.CreatedAt, user.UpdatedAt);
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        var password = hasher.VerifyHashedPassword(user, user.PasswordHash, "FirstSeed1!") == PasswordVerificationResult.Success
            ? "FirstSeed1!" : "SecondSeed1!";
        Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(user, user.PasswordHash, password));
        Assert.NotEqual(password, user.PasswordHash);

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { NationalIdentificationNumber = user.NationalIdentificationNumber!, Password = password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tokens = await login.Content.ReadFromJsonAsync<AuthenticationResponse>();
        Assert.NotNull(tokens);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/__tests/authorization/administrator");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        using var authorized = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, authorized.StatusCode);

        var originalHash = user.PasswordHash;
        user.Email = "existing-admin@example.com";
        user.AccountStatus = AccountStatus.Suspended;
        await db.SaveChangesAsync();
        await SeedAsync("Replace1!");
        await db.Entry(user).ReloadAsync();
        Assert.Equal("existing-admin@example.com", user.Email);
        Assert.Equal(AccountStatus.Suspended, user.AccountStatus);
        Assert.Equal(originalHash, user.PasswordHash);
        Assert.Equal(1, await db.Users.CountAsync());
        Assert.Equal(1, await db.Staff.CountAsync());
        Assert.Equal(1, await db.EmployeeNumbers.CountAsync());
    }

    [Fact]
    public async Task Seed_UsesSharedSequenceWithoutTruncatingEmployeeNumbers()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        await db.Database.ExecuteSqlRawAsync("ALTER SEQUENCE \"EmployeeNumberSequence\" RESTART WITH 1000000");
        await SeedAsync("Initial1!");
        Assert.Equal("EMP-1000000", (await db.Staff.SingleAsync()).EmployeeNumber);
        Assert.Equal("EMP-1000000", (await db.EmployeeNumbers.SingleAsync()).EmployeeNumber);
    }

    [Fact]
    public async Task Seed_WithEmailAlreadyOwnedByPatient_RollsBackWithoutChangingThatAccount()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var registration = await client.PostAsJsonAsync("/api/patients/register", new RegisterPatientRequest
        {
            FirstName = "Ana", LastName = "Perez", Email = "administrator0101@gmail.com", Password = "Ab1!_?xy",
            NationalIdentificationNumber = "01234567", PhoneNumber = "+598 99 123 456", DateOfBirth = new DateOnly(2000, 1, 1)
        });
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        await Assert.ThrowsAsync<DbUpdateException>(() => SeedAsync("Initial1!"));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        Assert.Equal(ProfileType.Patient, (await db.Users.SingleAsync()).ProfileType);
        Assert.Equal(1, await db.Patients.CountAsync());
        Assert.Equal(0, await db.Staff.CountAsync());
        Assert.Equal(0, await db.EmployeeNumbers.CountAsync());
    }

    [Fact]
    public async Task Seed_WithPasswordRejectedByLogin_DoesNotCreateAccounts()
    {
        foreach (var password in new[] { "", "short", "this-password-is-too-long" })
            await Assert.ThrowsAnyAsync<ArgumentException>(() => SeedAsync(password));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        Assert.Equal(0, await db.Users.CountAsync());
        Assert.Equal(0, await db.Staff.CountAsync());
        Assert.Equal(0, await db.EmployeeNumbers.CountAsync());
    }

    [Fact]
    public async Task Seed_WithInvalidIdentification_DoesNotCreateAccounts()
    {
        foreach (var identification in new[] { "", "1234567", "123456789", "0.123.456-7", " 12345678" })
        {
            using var scope = factory.Services.CreateScope();
            await Assert.ThrowsAnyAsync<ArgumentException>(() => DatabaseSeed.SeedAsync(
                scope.ServiceProvider.GetRequiredService<HospitalDbContext>(),
                scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>(), "Initial1!", identification));
        }
        using var verification = factory.Services.CreateScope();
        var db = verification.ServiceProvider.GetRequiredService<HospitalDbContext>();
        Assert.Equal(0, await db.Users.CountAsync());
        Assert.Equal(0, await db.Staff.CountAsync());
        Assert.Equal(0, await db.EmployeeNumbers.CountAsync());
    }

    private async Task SeedAsync(string password)
    {
        using var scope = factory.Services.CreateScope();
        await DatabaseSeed.SeedAsync(
            scope.ServiceProvider.GetRequiredService<HospitalDbContext>(),
            scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>(), password, "90000001");
    }
}
