using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HospitalPlatform.Api.Features.Authentication.DTOs;
using HospitalPlatform.Api.Features.Staff.DTOs;
using HospitalPlatform.Api.Features.Staff.Models;
using HospitalPlatform.Api.Features.Users.Models;
using HospitalPlatform.Api.Infrastructure.Persistence;
using HospitalPlatform.Api.Infrastructure.Persistence.Seeds;
using HospitalPlatform.Api.Tests.Shared.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalPlatform.Api.Tests.Functional.Features.Staff;

public class StaffRegistrationFlowTests : IAsyncLifetime
{
    private const string Password = "Ab1!_?xy";
    private readonly HospitalApiFactory factory = new();
    private HttpClient client = null!;

    public async ValueTask InitializeAsync()
    {
        await factory.InitializeAsync();
        client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var scope = factory.Services.CreateScope();
        await DatabaseSeed.SeedAsync(scope.ServiceProvider.GetRequiredService<HospitalDbContext>(),
            scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>(), Password, "90000001");
    }

    public async ValueTask DisposeAsync()
    {
        client?.Dispose();
        await factory.DisposeAsync();
    }

    [Theory]
    [InlineData(StaffRole.Receptionist)]
    [InlineData(StaffRole.Administrator)]
    public async Task AdministratorRegistersStaff_EmployeeCanLogInReadOwnProfileAndUseAssignedRole(StaffRole role)
    {
        var administratorTokens = await LoginAsync("90000001");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", administratorTokens.AccessToken);
        var request = new RegisterStaffRequest
        {
            FirstName = "Ana", LastName = "Perez", Email = "ana.staff@example.com", NationalIdentificationNumber = "12345678", Password = Password, StaffRole = role
        };
        using var registration = await client.PostAsJsonAsync("/api/staff/register", request);
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        var employee = await registration.Content.ReadFromJsonAsync<RegisterStaffResponse>();
        Assert.NotNull(employee);
        Assert.Equal("Active", employee.AccountStatus);
        Assert.Equal(role.ToString(), employee.StaffRole);

        var employeeTokens = await LoginAsync(request.NationalIdentificationNumber);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", employeeTokens.AccessToken);
        using var ownProfile = await client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.OK, ownProfile.StatusCode);
        using var profile = JsonDocument.Parse(await ownProfile.Content.ReadAsStringAsync());
        Assert.Equal(employee.UserId, profile.RootElement.GetProperty("userId").GetGuid());
        Assert.Equal(request.FirstName, profile.RootElement.GetProperty("firstName").GetString());
        Assert.Equal(request.Email, profile.RootElement.GetProperty("email").GetString());
        Assert.Equal("Staff", profile.RootElement.GetProperty("profileType").GetString());
        Assert.Equal("Active", profile.RootElement.GetProperty("accountStatus").GetString());

        request.Email = "another.staff@example.com";
        request.NationalIdentificationNumber = "23456789";
        request.StaffRole = StaffRole.Receptionist;
        using var staffOperation = await client.PostAsJsonAsync("/api/staff/register", request);
        Assert.Equal(role == StaffRole.Administrator ? HttpStatusCode.Created : HttpStatusCode.Forbidden, staffOperation.StatusCode);

        using var logout = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        using var afterLogout = await client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    private async Task<AuthenticationResponse> LoginAsync(string nationalIdentificationNumber)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { NationalIdentificationNumber = nationalIdentificationNumber, Password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tokens = await response.Content.ReadFromJsonAsync<AuthenticationResponse>();
        Assert.NotNull(tokens);
        return tokens;
    }
}
