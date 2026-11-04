using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HospitalPlatform.Api.Features.Authentication.DTOs;
using HospitalPlatform.Api.Features.Doctors.DTOs;
using HospitalPlatform.Api.Features.Patients.DTOs;
using HospitalPlatform.Api.Features.Staff.DTOs;
using HospitalPlatform.Api.Features.Staff.Models;
using HospitalPlatform.Api.Features.Users.Models;
using HospitalPlatform.Api.Infrastructure.Persistence;
using HospitalPlatform.Api.Infrastructure.Persistence.Seeds;
using HospitalPlatform.Api.Tests.Shared.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalPlatform.Api.Tests.Integration.Features.Doctors;

public class DoctorRegistrationTests : IAsyncLifetime
{
    private const string RegistrationUrl = "/api/doctors/register";
    private const string Password = "Ab1!_?xy";
    private int identificationSequence = 20000000;
    private readonly HospitalApiFactory factory = new();
    private HttpClient client = null!;

    public async ValueTask InitializeAsync()
    {
        await factory.InitializeAsync();
        client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var scope = factory.Services.CreateScope();
        await DatabaseSeed.SeedAsync(scope.ServiceProvider.GetRequiredService<HospitalDbContext>(),
            scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>(), Password, "90000001");
        await AuthorizeFixtureAccountAsync("administrator0101@gmail.com", Password);
    }

    public async ValueTask DisposeAsync()
    {
        client?.Dispose();
        await factory.DisposeAsync();
    }

    [Fact]
    public async Task Register_WithValidData_PersistsActiveDoctorAndOwnedEmployeeNumber()
    {
        var request = CreateRequest();
        using var response = await client.PostAsJsonAsync(RegistrationUrl, request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<RegisterDoctorResponse>();
        Assert.NotNull(result);
        Assert.Equal("Active", result.AccountStatus);
        Assert.Equal("EMP-000002", result.EmployeeNumber);
        Assert.Equal("00012345", result.MedicalLicenseNumber);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        var user = await db.Users.SingleAsync(user => user.Id == result.UserId);
        var doctor = await db.Doctors.SingleAsync(doctor => doctor.Id == result.DoctorId);
        var number = await db.EmployeeNumbers.SingleAsync(number => number.UserId == user.Id);
        Assert.Equal(request.FirstName, user.FirstName);
        Assert.Equal(request.LastName, user.LastName);
        Assert.Equal("medico+turnos@example.com", user.Email);
        Assert.Equal(ProfileType.Doctor, user.ProfileType);
        Assert.Equal(AccountStatus.Active, user.AccountStatus);
        Assert.Equal(user.Id, doctor.UserId);
        Assert.Equal(result.EmployeeNumber, doctor.EmployeeNumber);
        Assert.Equal(doctor.EmployeeNumber, number.EmployeeNumber);
        Assert.Equal(result.MedicalLicenseNumber, doctor.MedicalLicenseNumber);
        Assert.Equal(TimeSpan.Zero, user.CreatedAt.Offset);
        Assert.Equal(user.CreatedAt, user.UpdatedAt);
        Assert.NotEqual(request.Password, user.PasswordHash);
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password));
        Assert.Equal(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password.Trim()));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(5, json.RootElement.EnumerateObject().Count());
        Assert.False(json.RootElement.TryGetProperty("password", out _));
        Assert.False(json.RootElement.TryGetProperty("passwordHash", out _));
        Assert.Equal((2, 1, 2), await GetCountsAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid-token")]
    public async Task Register_WithoutValidAuthentication_ReturnsUnauthorized(string? token)
    {
        client.DefaultRequestHeaders.Authorization = token == null ? null : new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.PostAsJsonAsync(RegistrationUrl, CreateRequest());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal((1, 0, 1), await GetCountsAsync());
    }

    [Theory]
    [InlineData("Patient", AccountStatus.Active)]
    [InlineData("Doctor", AccountStatus.Active)]
    [InlineData("Receptionist", AccountStatus.Active)]
    [InlineData("Administrator", AccountStatus.PendingVerification)]
    [InlineData("Administrator", AccountStatus.Suspended)]
    [InlineData("Administrator", AccountStatus.Deactivated)]
    public async Task Register_WithoutActiveAdministratorPermissions_ReturnsForbidden(string role, AccountStatus status)
    {
        var email = "administrator0101@gmail.com";
        if (role != "Administrator")
        {
            email = role.ToLowerInvariant() + "@example.com";
            using var actor = await (role switch
            {
                "Patient" => client.PostAsJsonAsync("/api/patients/register", new RegisterPatientRequest
                {
                    FirstName = "Test", LastName = "Patient", Email = email, Password = Password,
                    NationalIdentificationNumber = "01234567", PhoneNumber = "+598 99 123 456", DateOfBirth = new DateOnly(2000, 1, 1)
                }),
                "Doctor" => client.PostAsJsonAsync(RegistrationUrl, CreateRequest(email, "ACTOR-001")),
                _ => client.PostAsJsonAsync("/api/staff/register", new RegisterStaffRequest
                {
                    FirstName = "Test", LastName = "Receptionist", Email = email, NationalIdentificationNumber = "33333333", Password = Password, StaffRole = StaffRole.Receptionist
                })
            });
            Assert.Equal(HttpStatusCode.Created, actor.StatusCode);
        }
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
            (await db.Users.SingleAsync(user => user.Email == email)).AccountStatus = status;
            await db.SaveChangesAsync();
        }
        await AuthorizeFixtureAccountAsync(email, role == "Doctor" ? CreateRequest().Password : Password);
        var before = await GetCountsAsync();
        using var response = await client.PostAsJsonAsync(RegistrationUrl, CreateRequest());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(before, await GetCountsAsync());
    }

    [Theory]
    [InlineData("firstName", "")]
    [InlineData("lastName", "A")]
    [InlineData("email", "invalid")]
    [InlineData("email", "medico turnos@example.com")]
    [InlineData("password", "12345")]
    [InlineData("password", "1234567890123")]
    [InlineData("nationalIdentificationNumber", "")]
    [InlineData("nationalIdentificationNumber", "0.123.456-7")]
    [InlineData("medicalLicenseNumber", "")]
    [InlineData("medicalLicenseNumber", " \t ")]
    public async Task Register_WithInvalidData_ReturnsBadRequestWithoutSaving(string field, string value)
    {
        var payload = JsonSerializer.SerializeToNode(CreateRequest(), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        payload[field] = value;
        using var response = await client.PostAsync(RegistrationUrl,
            new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal((1, 0, 1), await GetCountsAsync());
    }

    [Theory]
    [InlineData("{")]
    [InlineData("{}")]
    [InlineData("{\"firstName\":\"Maria\",\"lastName\":\"Gomez\",\"email\":\"medico@example.com\",\"password\":\"123456\"}")]
    public async Task Register_WithMalformedOrMissingData_ReturnsBadRequestWithoutSaving(string body)
    {
        using var response = await client.PostAsync(RegistrationUrl, new StringContent(body, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal((1, 0, 1), await GetCountsAsync());
    }

    [Theory]
    [InlineData(true, "Email is already registered.")]
    [InlineData(false, "Medical license number is already registered.")]
    public async Task Register_WithDuplicateIdentifier_ReturnsConflictAndRollsBack(bool duplicateEmail, string message)
    {
        using var original = await client.PostAsJsonAsync(RegistrationUrl, CreateRequest());
        Assert.Equal(HttpStatusCode.Created, original.StatusCode);
        var duplicate = duplicateEmail
            ? CreateRequest(" MEDICO+TURNOS@EXAMPLE.COM ", "OTHER-001")
            : CreateRequest("another@example.com", " 00012345 ");
        using var response = await client.PostAsJsonAsync(RegistrationUrl, duplicate);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(message, json.RootElement.GetProperty("message").GetString());
        Assert.Equal((2, 1, 2), await GetCountsAsync());
    }

    [Fact]
    public async Task Register_WithExistingStaffEmail_RollsBackAndDoesNotRecycleEmployeeNumber()
    {
        using var duplicate = await client.PostAsJsonAsync(RegistrationUrl, CreateRequest(" ADMINISTRATOR0101@GMAIL.COM "));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal((1, 0, 1), await GetCountsAsync());
        using var next = await client.PostAsJsonAsync(RegistrationUrl, CreateRequest());
        Assert.Equal(HttpStatusCode.Created, next.StatusCode);
        var result = await next.Content.ReadFromJsonAsync<RegisterDoctorResponse>();
        Assert.NotNull(result);
        Assert.Equal("EMP-000003", result.EmployeeNumber);
        Assert.Equal((2, 1, 2), await GetCountsAsync());
    }

    [Fact]
    public async Task Register_WithIdentificationOwnedByStaff_ReturnsConflictAndRollsBack()
    {
        var request = CreateRequest();
        request.NationalIdentificationNumber = "90000001";
        using var duplicate = await client.PostAsJsonAsync(RegistrationUrl, request);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal((1, 0, 1), await GetCountsAsync());
    }

    [Fact]
    public async Task Register_WithConcurrentStaffAndDoctorIdentification_CommitsOnlyOneAccount()
    {
        var doctor = CreateRequest();
        doctor.NationalIdentificationNumber = "01234567";
        var responses = await Task.WhenAll(
            client.PostAsJsonAsync(RegistrationUrl, doctor),
            client.PostAsJsonAsync("/api/staff/register", new RegisterStaffRequest
            {
                FirstName = "Test", LastName = "Receptionist", Email = "staff@example.com", Password = Password,
                NationalIdentificationNumber = "01234567", StaffRole = StaffRole.Receptionist
            }));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
            Assert.Equal(2, await db.Users.CountAsync());
            Assert.Equal(2, await db.EmployeeNumbers.CountAsync());
            Assert.Equal(2, await db.Doctors.CountAsync() + await db.Staff.CountAsync());
            Assert.Equal(1, await db.Users.CountAsync(user => user.NationalIdentificationNumber == "01234567"));
        }
        finally
        {
            foreach (var response in responses)
                response.Dispose();
        }
    }

    [Theory]
    [InlineData(AccountStatus.Suspended)]
    [InlineData(AccountStatus.Deactivated)]
    public async Task Register_WithLicenseOwnedByInactiveDoctor_ReturnsConflict(AccountStatus status)
    {
        using var original = await client.PostAsJsonAsync(RegistrationUrl, CreateRequest());
        Assert.Equal(HttpStatusCode.Created, original.StatusCode);
        var result = await original.Content.ReadFromJsonAsync<RegisterDoctorResponse>();
        Assert.NotNull(result);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        (await db.Users.SingleAsync(user => user.Id == result.UserId)).AccountStatus = status;
        await db.SaveChangesAsync();

        using var duplicate = await client.PostAsJsonAsync(RegistrationUrl, CreateRequest("another@example.com"));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal((2, 1, 2), await GetCountsAsync());
        Assert.Equal(result.EmployeeNumber, (await db.Doctors.SingleAsync()).EmployeeNumber);
    }

    [Fact]
    public async Task Register_WithClientSuppliedOwnershipAndStatus_UsesServerValues()
    {
        var payload = JsonSerializer.SerializeToNode(CreateRequest(), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var suppliedId = Guid.NewGuid();
        payload["userId"] = suppliedId;
        payload["doctorId"] = suppliedId;
        payload["employeeNumber"] = "EMP-999999";
        payload["profileType"] = "Staff";
        payload["accountStatus"] = "Suspended";
        using var response = await client.PostAsync(RegistrationUrl,
            new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<RegisterDoctorResponse>();
        Assert.NotNull(result);
        Assert.NotEqual(suppliedId, result.UserId);
        Assert.NotEqual(suppliedId, result.DoctorId);
        Assert.Equal("EMP-000002", result.EmployeeNumber);
        Assert.Equal("Active", result.AccountStatus);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        Assert.Equal(ProfileType.Doctor, (await db.Users.SingleAsync(user => user.Id == result.UserId)).ProfileType);
        Assert.Equal((2, 1, 2), await GetCountsAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Register_WithConcurrentDuplicateIdentifier_CommitsOnlyOneCompleteRegistration(bool duplicateEmail)
    {
        var responses = await Task.WhenAll(
            client.PostAsJsonAsync(RegistrationUrl, CreateRequest("same@example.com", "LIC-001")),
            client.PostAsJsonAsync(RegistrationUrl, duplicateEmail
                ? CreateRequest(" SAME@EXAMPLE.COM ", "LIC-002")
                : CreateRequest("other@example.com", " LIC-001 ")));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal((2, 1, 2), await GetCountsAsync());
        }
        finally
        {
            foreach (var response in responses)
                response.Dispose();
        }
    }

    [Fact]
    public async Task Register_ConcurrentDoctorAndStaffRequests_UseSharedUntruncatedEmployeeNumbers()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
            await db.Database.ExecuteSqlRawAsync("ALTER SEQUENCE \"EmployeeNumberSequence\" RESTART WITH 1000000");
        }
        var responses = await Task.WhenAll(
            client.PostAsJsonAsync(RegistrationUrl, CreateRequest("first@example.com", "LIC-001")),
            client.PostAsJsonAsync(RegistrationUrl, CreateRequest("second@example.com", "LIC-002")),
            client.PostAsJsonAsync("/api/staff/register", new RegisterStaffRequest
            {
                FirstName = "Test", LastName = "Receptionist", Email = "staff@example.com", NationalIdentificationNumber = "33333333", Password = Password, StaffRole = StaffRole.Receptionist
            }));
        try
        {
            foreach (var response in responses)
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
            var numbers = await db.EmployeeNumbers.Select(number => number.EmployeeNumber).ToListAsync();
            Assert.Equal(new[] { "EMP-000001", "EMP-1000000", "EMP-1000001", "EMP-1000002" }, numbers.OrderBy(number => number));
            Assert.Equal((4, 2, 4), await GetCountsAsync());
            Assert.Equal(2, await db.Staff.CountAsync());
        }
        finally
        {
            foreach (var response in responses)
                response.Dispose();
        }
    }

    private async Task AuthorizeFixtureAccountAsync(string email, string password)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        var identificationNumber = await db.Users.Where(user => user.Email == email)
            .Select(user => user.NationalIdentificationNumber).SingleAsync();
        using var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { NationalIdentificationNumber = identificationNumber!, Password = password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tokens = await response.Content.ReadFromJsonAsync<AuthenticationResponse>();
        Assert.NotNull(tokens);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
    }

    private async Task<(int Users, int Doctors, int Numbers)> GetCountsAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        return (await db.Users.CountAsync(), await db.Doctors.CountAsync(), await db.EmployeeNumbers.CountAsync());
    }

    private RegisterDoctorRequest CreateRequest(string email = " Medico+turnos@Example.com ", string license = " 00012345 ") => new()
    {
        FirstName = "Maria", LastName = "Gomez", Email = email, NationalIdentificationNumber = Interlocked.Increment(ref identificationSequence).ToString("D8"), Password = " Ab1!_? ", MedicalLicenseNumber = license
    };
}
