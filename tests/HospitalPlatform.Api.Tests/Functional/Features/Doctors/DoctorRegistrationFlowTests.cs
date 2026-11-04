using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HospitalPlatform.Api.Features.Authentication.DTOs;
using HospitalPlatform.Api.Features.Doctors.DTOs;
using HospitalPlatform.Api.Features.Users.Models;
using HospitalPlatform.Api.Infrastructure.Persistence;
using HospitalPlatform.Api.Infrastructure.Persistence.Seeds;
using HospitalPlatform.Api.Tests.Shared.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalPlatform.Api.Tests.Functional.Features.Doctors;

public class DoctorRegistrationFlowTests : IAsyncLifetime
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

    [Fact]
    public async Task AdministratorRegistersDoctor_DoctorCanLogInReadOwnAccountAndLogOut()
    {
        await AuthorizeAsync("90000001");
        var request = new RegisterDoctorRequest
        {
            FirstName = "Maria", LastName = "Gomez", Email = "medico@example.com", NationalIdentificationNumber = "12345678", Password = Password, MedicalLicenseNumber = "00012345"
        };
        using var registration = await client.PostAsJsonAsync("/api/doctors/register", request);
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        var doctor = await registration.Content.ReadFromJsonAsync<RegisterDoctorResponse>();
        Assert.NotNull(doctor);
        Assert.Equal("Active", doctor.AccountStatus);
        Assert.Equal(request.MedicalLicenseNumber, doctor.MedicalLicenseNumber);
        Assert.StartsWith("EMP-", doctor.EmployeeNumber);

        await AuthorizeAsync(request.NationalIdentificationNumber);
        using var ownProfile = await client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.OK, ownProfile.StatusCode);
        using var profile = JsonDocument.Parse(await ownProfile.Content.ReadAsStringAsync());
        Assert.Equal(doctor.UserId, profile.RootElement.GetProperty("userId").GetGuid());
        Assert.Equal(request.FirstName, profile.RootElement.GetProperty("firstName").GetString());
        Assert.Equal(request.Email, profile.RootElement.GetProperty("email").GetString());
        Assert.Equal("Doctor", profile.RootElement.GetProperty("profileType").GetString());
        Assert.Equal("Active", profile.RootElement.GetProperty("accountStatus").GetString());

        request.Email = "another@example.com";
        request.NationalIdentificationNumber = "23456789";
        request.MedicalLicenseNumber = "OTHER-001";
        using var denied = await client.PostAsJsonAsync("/api/doctors/register", request);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var logout = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        using var afterLogout = await client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    private async Task AuthorizeAsync(string nationalIdentificationNumber)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { NationalIdentificationNumber = nationalIdentificationNumber, Password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tokens = await response.Content.ReadFromJsonAsync<AuthenticationResponse>();
        Assert.NotNull(tokens);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
    }
}
