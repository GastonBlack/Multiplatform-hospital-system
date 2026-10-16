using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HospitalPlatform.Api.Features.Authentication.DTOs;
using HospitalPlatform.Api.Features.Patients.DTOs;
using HospitalPlatform.Api.Features.Staff.Models;
using HospitalPlatform.Api.Features.Users.Models;
using HospitalPlatform.Api.Infrastructure.Persistence;
using HospitalPlatform.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalPlatform.Api.IntegrationTests.Features.Patients;

public class PatientProfileTests : IAsyncLifetime
{
    private readonly HospitalApiFactory factory = new();
    private HttpClient client = null!;

    public async ValueTask InitializeAsync()
    {
        await factory.InitializeAsync();
        client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
    }

    public async ValueTask DisposeAsync()
    {
        client?.Dispose();
        await factory.DisposeAsync();
    }

    [Fact]
    public async Task GetProfile_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var response = await client.GetAsync("/api/patients/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(AccountStatus.PendingVerification)]
    [InlineData(AccountStatus.Active)]
    [InlineData(AccountStatus.Suspended)]
    [InlineData(AccountStatus.Deactivated)]
    public async Task GetProfile_ReturnsOnlyCurrentPatientInEveryAccountStatus(AccountStatus status)
    {
        var request = new RegisterPatientRequest
        {
            FirstName = "Ana", LastName = "Perez", Email = "ana@example.com", Password = "Ab1!_?xy",
            NationalIdentificationNumber = "01234567", PhoneNumber = "+598 99 123 456", DateOfBirth = new DateOnly(2000, 1, 1)
        };
        using var registration = await client.PostAsJsonAsync("/api/patients/register", request);
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);

        request.FirstName = "Bruno";
        request.Email = "bruno@example.com";
        request.NationalIdentificationNumber = "87654321";
        using var otherRegistration = await client.PostAsJsonAsync("/api/patients/register", request);
        Assert.Equal(HttpStatusCode.Created, otherRegistration.StatusCode);

        var receptionist = await CreateStaffAsync(StaffRole.Receptionist);
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
            var user = await db.Users.SingleAsync(user => user.Email == "ana@example.com");
            var patient = await db.Patients.SingleAsync(patient => patient.UserId == user.Id);
            user.AccountStatus = status;
            if (status != AccountStatus.PendingVerification)
            {
                patient.IdentityVerifiedAt = DateTimeOffset.UtcNow;
                patient.IdentityVerifiedByUserId = receptionist.Id;
            }
            await db.SaveChangesAsync();
        }

        await AuthenticateAsync("ana@example.com");
        using var response = await client.GetAsync("/api/patients/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<GetPatientProfile>();
        Assert.NotNull(profile);
        Assert.Equal("Ana", profile.FirstName);
        Assert.Equal("Perez", profile.LastName);
        Assert.Equal("ana@example.com", profile.Email);
        Assert.Equal("01234567", profile.NationalIdentificationNumber);
        Assert.Equal(request.PhoneNumber, profile.PhoneNumber);
        Assert.Equal(request.DateOfBirth, profile.DateOfBirth);
        Assert.Equal(status.ToString(), profile.AccountStatus);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(7, json.RootElement.EnumerateObject().Count());
        Assert.False(json.RootElement.TryGetProperty("password", out _));
        Assert.False(json.RootElement.TryGetProperty("passwordHash", out _));
    }

    [Theory]
    [InlineData(StaffRole.Receptionist)]
    [InlineData(StaffRole.Administrator)]
    public async Task GetProfile_WithStaffAccount_ReturnsForbidden(StaffRole role)
    {
        var staff = await CreateStaffAsync(role);
        await AuthenticateAsync(staff.Email);
        using var response = await client.GetAsync("/api/patients/me");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task AuthenticateAsync(string email)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = "Ab1!_?xy" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tokens = await response.Content.ReadFromJsonAsync<AuthenticationResponse>();
        Assert.NotNull(tokens);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
    }

    private async Task<User> CreateStaffAsync(StaffRole role)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            FirstName = "Staff", LastName = "Test", Email = "staff@example.com", PasswordHash = string.Empty,
            ProfileType = ProfileType.Staff, AccountStatus = AccountStatus.Active, CreatedAt = now, UpdatedAt = now
        };
        user.PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>().HashPassword(user, "Ab1!_?xy");
        db.Users.Add(user);
        db.EmployeeNumbers.Add(new EmployeeNumberRegistration { UserId = user.Id, EmployeeNumber = "EMP-000001" });
        db.Staff.Add(new Staff { UserId = user.Id, EmployeeNumber = "EMP-000001", StaffRole = role });
        await db.SaveChangesAsync();
        return user;
    }
}
