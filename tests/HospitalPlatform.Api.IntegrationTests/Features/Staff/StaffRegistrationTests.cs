using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HospitalPlatform.Api.Features.Authentication.DTOs;
using HospitalPlatform.Api.Features.Doctors.Models;
using HospitalPlatform.Api.Features.Patients.DTOs;
using HospitalPlatform.Api.Features.Patients.Models;
using HospitalPlatform.Api.Features.Staff.DTOs;
using HospitalPlatform.Api.Features.Staff.Models;
using HospitalPlatform.Api.Features.Users.Models;
using HospitalPlatform.Api.Features.Users.Services;
using HospitalPlatform.Api.Infrastructure.Persistence;
using HospitalPlatform.Api.Infrastructure.Persistence.Seeds;
using HospitalPlatform.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalPlatform.Api.IntegrationTests.Features;

public class StaffRegistrationTests : IAsyncLifetime
{
    private const string RegistrationUrl = "/api/staff/register";
    private const string Password = "Ab1!_?xy";
    private readonly HospitalApiFactory factory = new();
    private HttpClient client = null!;

    public async ValueTask InitializeAsync()
    {
        await factory.InitializeAsync();
        client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var scope = factory.Services.CreateScope();
        await DatabaseSeed.SeedAsync(scope.ServiceProvider.GetRequiredService<HospitalDbContext>(),
            scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>(), Password);
        var tokens = await LoginAsync("administrator0101@gmail.com", Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
    }

    public async ValueTask DisposeAsync()
    {
        client?.Dispose();
        await factory.DisposeAsync();
    }

    [Theory]
    [InlineData(StaffRole.Receptionist)]
    [InlineData(StaffRole.Administrator)]
    public async Task Register_WithValidData_CreatesActiveStaffThatCanLogIn(StaffRole role)
    {
        var request = CreateRequest(role: role);
        using var response = await client.PostAsJsonAsync(RegistrationUrl, request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<RegisterStaffResponse>();
        Assert.NotNull(result);
        Assert.Equal("Active", result.AccountStatus);
        Assert.Equal(role.ToString(), result.StaffRole);
        Assert.Equal("EMP-000002", result.EmployeeNumber);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        var user = await db.Users.SingleAsync(user => user.Id == result.UserId);
        var staff = await db.Staff.SingleAsync(staff => staff.Id == result.StaffId);
        var number = await db.EmployeeNumbers.SingleAsync(number => number.UserId == user.Id);
        Assert.Equal(request.FirstName, user.FirstName);
        Assert.Equal(request.LastName, user.LastName);
        Assert.Equal("ana+staff@example.com", user.Email);
        Assert.Equal(ProfileType.Staff, user.ProfileType);
        Assert.Equal(AccountStatus.Active, user.AccountStatus);
        Assert.Equal(user.Id, staff.UserId);
        Assert.Equal(role, staff.StaffRole);
        Assert.Equal(result.EmployeeNumber, staff.EmployeeNumber);
        Assert.Equal(staff.EmployeeNumber, number.EmployeeNumber);
        Assert.Equal(TimeSpan.Zero, user.CreatedAt.Offset);
        Assert.Equal(user.CreatedAt, user.UpdatedAt);
        Assert.NotEqual(request.Password, user.PasswordHash);
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(5, json.RootElement.EnumerateObject().Count());
        Assert.False(json.RootElement.TryGetProperty("password", out _));
        Assert.False(json.RootElement.TryGetProperty("passwordHash", out _));
        Assert.Equal((2, 2, 2, 0, 0), await GetCountsAsync());

        var tokens = await LoginAsync(user.Email, request.Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        using var ownProfile = await client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.OK, ownProfile.StatusCode);
        using var profileJson = JsonDocument.Parse(await ownProfile.Content.ReadAsStringAsync());
        Assert.Equal(user.Id, profileJson.RootElement.GetProperty("userId").GetGuid());
        using var administrator = await client.GetAsync("/__tests/authorization/administrator");
        Assert.Equal(role == StaffRole.Administrator ? HttpStatusCode.OK : HttpStatusCode.Forbidden, administrator.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid-token")]
    public async Task Register_WithoutValidAuthentication_ReturnsUnauthorized(string? token)
    {
        client.DefaultRequestHeaders.Authorization = token == null ? null : new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.PostAsJsonAsync(RegistrationUrl, CreateRequest());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal((1, 1, 1, 0, 0), await GetCountsAsync());
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
        var user = await CreateUserAsync(role, status);
        var tokens = await LoginAsync(user.Email, Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        var before = await GetCountsAsync();
        using var response = await client.PostAsJsonAsync(RegistrationUrl, CreateRequest());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(before, await GetCountsAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Register_AfterAdministratorPermissionsChange_RejectsExistingToken(bool changeRole)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        if (changeRole)
            (await db.Staff.SingleAsync()).StaffRole = StaffRole.Receptionist;
        else
            (await db.Users.SingleAsync()).AccountStatus = AccountStatus.Suspended;
        await db.SaveChangesAsync();

        using var response = await client.PostAsJsonAsync(RegistrationUrl, CreateRequest());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal((1, 1, 1, 0, 0), await GetCountsAsync());
    }

    [Theory]
    [InlineData("firstName", "")]
    [InlineData("firstName", "A")]
    [InlineData("lastName", " ")]
    [InlineData("lastName", "P")]
    [InlineData("email", "invalid")]
    [InlineData("email", "ana staff@example.com")]
    [InlineData("password", "12345")]
    [InlineData("password", "1234567890123")]
    [InlineData("staffRole", "Patient")]
    [InlineData("staffRole", "Doctor")]
    [InlineData("staffRole", "0")]
    [InlineData("staffRole", "99")]
    public async Task Register_WithInvalidData_ReturnsBadRequestWithoutSaving(string field, string value)
    {
        var payload = JsonSerializer.SerializeToNode(CreateRequest(), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        payload[field] = value;
        using var response = await client.PostAsync(RegistrationUrl,
            new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal((1, 1, 1, 0, 0), await GetCountsAsync());
    }

    [Theory]
    [InlineData("{")]
    [InlineData("{}")]
    [InlineData("{\"firstName\":\"Ana\",\"lastName\":\"Perez\",\"email\":\"ana@example.com\",\"password\":\"123456\"}")]
    public async Task Register_WithMalformedOrMissingData_ReturnsBadRequestWithoutSaving(string body)
    {
        using var response = await client.PostAsync(RegistrationUrl, new StringContent(body, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal((1, 1, 1, 0, 0), await GetCountsAsync());
    }

    [Fact]
    public async Task Register_WithExistingPatientEmail_ReturnsConflictAndRollsBack()
    {
        using var patient = await client.PostAsJsonAsync("/api/patients/register", new RegisterPatientRequest
        {
            FirstName = "Ana", LastName = "Perez", Email = "ana+staff@example.com", Password = Password,
            NationalIdentificationNumber = "01234567", PhoneNumber = "+598 99 123 456", DateOfBirth = new DateOnly(2000, 1, 1)
        });
        Assert.Equal(HttpStatusCode.Created, patient.StatusCode);
        using var response = await client.PostAsJsonAsync(RegistrationUrl, CreateRequest());
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Email is already registered.", json.RootElement.GetProperty("message").GetString());
        Assert.Equal((2, 1, 1, 1, 0), await GetCountsAsync());

        using var next = await client.PostAsJsonAsync(RegistrationUrl, CreateRequest("another@example.com"));
        Assert.Equal(HttpStatusCode.Created, next.StatusCode);
        var result = await next.Content.ReadFromJsonAsync<RegisterStaffResponse>();
        Assert.NotNull(result);
        Assert.Equal("EMP-000003", result.EmployeeNumber);
        Assert.Equal((3, 2, 2, 1, 0), await GetCountsAsync());
    }

    [Fact]
    public async Task Register_WithClientSuppliedOwnershipAndStatus_UsesServerValues()
    {
        var payload = JsonSerializer.SerializeToNode(CreateRequest(), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var suppliedId = Guid.NewGuid();
        payload["userId"] = suppliedId;
        payload["staffId"] = suppliedId;
        payload["employeeNumber"] = "EMP-999999";
        payload["profileType"] = "Doctor";
        payload["accountStatus"] = "Suspended";
        using var response = await client.PostAsync(RegistrationUrl,
            new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<RegisterStaffResponse>();
        Assert.NotNull(result);
        Assert.NotEqual(suppliedId, result.UserId);
        Assert.NotEqual(suppliedId, result.StaffId);
        Assert.Equal("EMP-000002", result.EmployeeNumber);
        Assert.Equal("Active", result.AccountStatus);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        Assert.Equal(ProfileType.Staff, (await db.Users.SingleAsync(user => user.Id == result.UserId)).ProfileType);
        Assert.Equal((2, 2, 2, 0, 0), await GetCountsAsync());
    }

    [Fact]
    public async Task Register_WithConcurrentDuplicateEmails_CommitsOnlyOneCompleteRegistration()
    {
        var responses = await Task.WhenAll(
            client.PostAsJsonAsync(RegistrationUrl, CreateRequest("same@example.com")),
            client.PostAsJsonAsync(RegistrationUrl, CreateRequest(" SAME@EXAMPLE.COM ")));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal((2, 2, 2, 0, 0), await GetCountsAsync());
        }
        finally
        {
            foreach (var response in responses)
                response.Dispose();
        }
    }

    [Fact]
    public async Task Register_ConcurrentStaffAndDoctorAllocation_UsesUniqueUntruncatedNumbers()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
            await db.Database.ExecuteSqlRawAsync("ALTER SEQUENCE \"EmployeeNumberSequence\" RESTART WITH 1000000");
        }

        var doctorTask = CreateUserAsync("Doctor", AccountStatus.Active);
        var responses = await Task.WhenAll(
            client.PostAsJsonAsync(RegistrationUrl, CreateRequest("first@example.com")),
            client.PostAsJsonAsync(RegistrationUrl, CreateRequest("second@example.com", StaffRole.Administrator)));
        await doctorTask;
        try
        {
            foreach (var response in responses)
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
            var numbers = await db.EmployeeNumbers.Select(number => number.EmployeeNumber).ToListAsync();
            Assert.Equal(new[] { "EMP-000001", "EMP-1000000", "EMP-1000001", "EMP-1000002" }, numbers.OrderBy(number => number));
            var staff = await db.Staff.ToListAsync();
            Assert.All(staff, member => Assert.Contains(member.EmployeeNumber, numbers));
            Assert.Equal((4, 3, 4, 0, 1), await GetCountsAsync());
        }
        finally
        {
            foreach (var response in responses)
                response.Dispose();
        }
    }

    private async Task<AuthenticationResponse> LoginAsync(string email, string password)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthenticationResponse>())!;
    }

    private async Task<(int Users, int Staff, int Numbers, int Patients, int Doctors)> GetCountsAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        return (await db.Users.CountAsync(), await db.Staff.CountAsync(), await db.EmployeeNumbers.CountAsync(),
            await db.Patients.CountAsync(), await db.Doctors.CountAsync());
    }

    private async Task<User> CreateUserAsync(string role, AccountStatus status)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            FirstName = "Test", LastName = "User", Email = role.ToLowerInvariant() + "@example.com", PasswordHash = string.Empty,
            ProfileType = role == "Patient" ? ProfileType.Patient : role == "Doctor" ? ProfileType.Doctor : ProfileType.Staff,
            AccountStatus = status, CreatedAt = now, UpdatedAt = now
        };
        user.PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>().HashPassword(user, Password);
        db.Users.Add(user);
        if (user.ProfileType == ProfileType.Patient)
            db.Patients.Add(new Patient
            {
                UserId = user.Id, NationalIdentificationNumber = "01234567",
                PhoneNumber = "+598 99 123 456", DateOfBirth = new DateOnly(2000, 1, 1)
            });
        else
        {
            var employeeNumber = await EmployeeNumberService.RegisterAsync(db, user.Id);
            if (user.ProfileType == ProfileType.Doctor)
                db.Doctors.Add(new Doctor { UserId = user.Id, EmployeeNumber = employeeNumber, MedicalLicenseNumber = "LICENSE-TEST" });
            else
                db.Staff.Add(new Staff { UserId = user.Id, EmployeeNumber = employeeNumber, StaffRole = Enum.Parse<StaffRole>(role) });
        }
        await db.SaveChangesAsync();
        return user;
    }

    private static RegisterStaffRequest CreateRequest(string email = " Ana+staff@Example.com ", StaffRole role = StaffRole.Receptionist) => new()
    {
        FirstName = "Ana", LastName = "Perez", Email = email, Password = " Ab1!_? ", StaffRole = role
    };
}
