using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HospitalPlatform.Api.Features.Patients.DTOs;
using HospitalPlatform.Api.Features.Users.Models;
using HospitalPlatform.Api.Infrastructure.Persistence;
using HospitalPlatform.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalPlatform.Api.IntegrationTests.Features.Patients;

public class PatientRegistrationTests : IAsyncLifetime
{
    private const string RegistrationUrl = "/api/patients/register";
    private readonly HospitalApiFactory factory = new();
    private HttpClient client = null!;

    // xUnit creates a new test class instance and database for every test case.
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
    public async Task RegisterPatient_WithNoSurname()
    {
        var request = CreateRequest();
        request.LastName = "";

        using var response = await client.PostAsJsonAsync(
            RegistrationUrl,
            request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertCountsAsync(0);
    }

    [Fact]
    public async Task RegisterPatient_WithInvalidBornDate()
    {
        var request = CreateRequest();

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Montevideo");
        var today = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone).DateTime);

        request.DateOfBirth = today.AddDays(1);

        using var response = await client.PostAsJsonAsync(
            RegistrationUrl,
            request
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Contains(
            json.RootElement.GetProperty("errors").EnumerateObject(),
            error => error.Name == nameof(RegisterPatientRequest.DateOfBirth));

        await AssertCountsAsync(0);
    }

    [Fact]
    public async Task Register_WithValidData_CreatesAccountAndUnverifiedProfile()
    {
        var request = CreateRequest();
        using var response = await client.PostAsJsonAsync(RegistrationUrl, request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<RegisterPatientResponse>();
        Assert.NotNull(result);
        Assert.Equal(AccountStatus.PendingVerification, result.AccountStatus);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        var user = await dbContext.Users.SingleAsync();
        var patient = await dbContext.Patients.SingleAsync();
        Assert.Equal(user.Id, result.UserId);
        Assert.Equal(patient.Id, result.PatientId);
        Assert.Equal(user.Id, patient.UserId);
        Assert.Equal("ana+turnos@example.com", user.Email);
        Assert.Equal(ProfileType.Patient, user.ProfileType);
        Assert.Equal(AccountStatus.PendingVerification, user.AccountStatus);
        Assert.Equal("01234567", patient.NationalIdentificationNumber);
        Assert.Null(patient.IdentityVerifiedAt);
        Assert.Null(patient.IdentityVerifiedByUserId);
        Assert.Equal(TimeSpan.Zero, user.CreatedAt.Offset);
        Assert.Equal(user.CreatedAt, user.UpdatedAt);
        Assert.NotEqual(request.Password, user.PasswordHash);
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password));
        Assert.Equal(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(user, user.PasswordHash, "wrong-password"));

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(3, json.RootElement.EnumerateObject().Count());
        Assert.False(json.RootElement.TryGetProperty("password", out _));
        Assert.False(json.RootElement.TryGetProperty("passwordHash", out _));
    }

    [Theory]
    [InlineData("firstName", "")]
    [InlineData("password", "12345")]
    [InlineData("password", "1234567890123")]
    [InlineData("email", "ana perez@example.com")]
    [InlineData("nationalIdentificationNumber", "0.123.456-7")]
    [InlineData("nationalIdentificationNumber", "01234567\n")]
    [InlineData("phoneNumber", "abcdef")]
    [InlineData("dateOfBirth", "0001-01-01")]
    public async Task Register_WithInvalidData_ReturnsBadRequestWithoutSaving(string field, string value)
    {
        var payload = JsonSerializer.SerializeToNode(CreateRequest(), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        payload[field] = value;
        using var response = await client.PostAsync(RegistrationUrl, new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Contains(json.RootElement.GetProperty("errors").EnumerateObject(), error => error.Name.Equals(field, StringComparison.OrdinalIgnoreCase));
        await AssertCountsAsync(0);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("{}")]
    public async Task Register_WithMalformedOrMissingData_ReturnsBadRequestWithoutSaving(string body)
    {
        using var response = await client.PostAsync(RegistrationUrl, new StringContent(body, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertCountsAsync(0);
    }

    [Theory]
    [InlineData(true, "Email is already registered.")]
    [InlineData(false, "Identification number is already registered.")]
    public async Task Register_WithDuplicateIdentifier_ReturnsConflictAndRollsBack(bool duplicateEmail, string message)
    {
        using var original = await client.PostAsJsonAsync(RegistrationUrl, CreateRequest());
        Assert.Equal(HttpStatusCode.Created, original.StatusCode);
        var duplicate = duplicateEmail
            ? CreateRequest("ANA+TURNOS@EXAMPLE.COM", "87654321")
            : CreateRequest("another@example.com", "01234567");
        using var response = await client.PostAsJsonAsync(RegistrationUrl, duplicate);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("conflict", json.RootElement.GetProperty("error").GetString());
        Assert.Equal(message, json.RootElement.GetProperty("message").GetString());
        await AssertCountsAsync(1);
    }

    [Fact]
    public async Task Register_WithConcurrentDuplicateEmails_CommitsOnlyOneAccount()
    {
        var responses = await Task.WhenAll(
            client.PostAsJsonAsync(RegistrationUrl, CreateRequest("same@example.com", "22222222")),
            client.PostAsJsonAsync(RegistrationUrl, CreateRequest("SAME@EXAMPLE.COM", "33333333")));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
            await AssertCountsAsync(1);
        }
        finally
        {
            foreach (var response in responses)
                response.Dispose();
        }
    }

    private async Task AssertCountsAsync(int expected)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        Assert.Equal(expected, await dbContext.Users.CountAsync());
        Assert.Equal(expected, await dbContext.Patients.CountAsync());
    }

    private static RegisterPatientRequest CreateRequest(string email = " Ana+turnos@Example.com ", string identificationNumber = "01234567") => new()
    {
        FirstName = "Ana",
        LastName = "Perez",
        Email = email,
        Password = " Ab1!_? ",
        NationalIdentificationNumber = identificationNumber,
        PhoneNumber = "+598 99 123 456",
        DateOfBirth = new DateOnly(2000, 1, 1)
    };
}
