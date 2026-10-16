using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HospitalPlatform.Api.Features.Authentication.DTOs;
using HospitalPlatform.Api.Features.Doctors.Models;
using HospitalPlatform.Api.Features.Patients.Models;
using HospitalPlatform.Api.Features.Staff.Models;
using HospitalPlatform.Api.Features.Users.Models;
using HospitalPlatform.Api.Infrastructure.Persistence;
using HospitalPlatform.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalPlatform.Api.IntegrationTests.Features.Users;

public class UserProfileTests : IAsyncLifetime
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

    [Theory]
    [InlineData(null)]
    [InlineData("invalid-token")]
    public async Task GetProfile_WithoutValidAuthentication_ReturnsUnauthorized(string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/users/me");
        if (token != null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetProfile_AllRolesAndStatuses_ReadOnlyTheirOwnAccount()
    {
        var number = 0;
        foreach (var role in new[] { "Patient", "Doctor", "Receptionist", "Administrator" })
        foreach (var status in Enum.GetValues<AccountStatus>())
        {
            if (status == AccountStatus.PendingVerification && role != "Patient")
                continue;
            number++;
            var user = await CreateUserAsync(role, status, number);
            using var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = user.Email, Password = "Ab1!_?xy" });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            var tokens = await login.Content.ReadFromJsonAsync<AuthenticationResponse>();
            Assert.NotNull(tokens);
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/users/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var profile = json.RootElement;
            Assert.Equal(user.Id, profile.GetProperty("userId").GetGuid());
            Assert.Equal(user.FirstName, profile.GetProperty("firstName").GetString());
            Assert.Equal(user.LastName, profile.GetProperty("lastName").GetString());
            Assert.Equal(user.Email, profile.GetProperty("email").GetString());
            Assert.Equal(user.ProfileType.ToString(), profile.GetProperty("profileType").GetString());
            Assert.Equal(status.ToString(), profile.GetProperty("accountStatus").GetString());
            Assert.Equal(6, profile.EnumerateObject().Count());
            if (status != AccountStatus.Active)
            {
                using var denied = new HttpRequestMessage(HttpMethod.Get, "/__tests/authorization/active");
                denied.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
                using var deniedResponse = await client.SendAsync(denied);
                Assert.Equal(HttpStatusCode.Forbidden, deniedResponse.StatusCode);
            }
            using var logout = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
            logout.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
            using var logoutResponse = await client.SendAsync(logout);
            Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
            using var revoked = new HttpRequestMessage(HttpMethod.Get, "/api/users/me");
            revoked.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
            using var revokedResponse = await client.SendAsync(revoked);
            Assert.Equal(HttpStatusCode.Unauthorized, revokedResponse.StatusCode);
        }
    }

    private async Task<User> CreateUserAsync(string role, AccountStatus status, int number)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            FirstName = "User" + number, LastName = "Test", Email = $"user{number}@example.com", PasswordHash = string.Empty,
            ProfileType = role == "Patient" ? ProfileType.Patient : role == "Doctor" ? ProfileType.Doctor : ProfileType.Staff,
            AccountStatus = status, CreatedAt = now, UpdatedAt = now
        };
        user.PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>().HashPassword(user, "Ab1!_?xy");
        db.Users.Add(user);
        if (user.ProfileType == ProfileType.Patient)
        {
            db.Patients.Add(new Patient
            {
                UserId = user.Id, NationalIdentificationNumber = number.ToString("D8"),
                PhoneNumber = "+598 99 123 456", DateOfBirth = new DateOnly(2000, 1, 1)
            });
        }
        else
        {
            var employeeNumber = $"EMP-{number:D6}";
            db.EmployeeNumbers.Add(new EmployeeNumberRegistration { UserId = user.Id, EmployeeNumber = employeeNumber });
            if (user.ProfileType == ProfileType.Doctor)
                db.Doctors.Add(new Doctor { UserId = user.Id, EmployeeNumber = employeeNumber, MedicalLicenseNumber = "LICENSE-" + number });
            else
                db.Staff.Add(new Staff { UserId = user.Id, EmployeeNumber = employeeNumber, StaffRole = Enum.Parse<StaffRole>(role) });
        }
        await db.SaveChangesAsync();
        return user;
    }
}
