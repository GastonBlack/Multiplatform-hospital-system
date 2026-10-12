using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HospitalPlatform.Api.Features.Authentication.DTOs;
using HospitalPlatform.Api.Features.Authentication.Settings;
using HospitalPlatform.Api.Features.Patients.DTOs;
using HospitalPlatform.Api.Features.Staff.Models;
using HospitalPlatform.Api.Features.Users.Models;
using HospitalPlatform.Api.Infrastructure.Persistence;
using HospitalPlatform.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HospitalPlatform.Api.IntegrationTests.Features.Authentication;

public class AuthenticationTests : IAsyncLifetime
{
    private readonly HospitalApiFactory _factory = new();
    private HttpClient _client = null!;

    // //////////////////////////////////////////
    // Test setup
    // //////////////////////////////////////////
    public async ValueTask InitializeAsync()
    {
        await _factory.InitializeAsync();
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var registration = await _client.PostAsJsonAsync("/api/patients/register", new RegisterPatientRequest
        {
            FirstName = "Ana", LastName = "Perez", Email = "ana@example.com", Password = " Ab1!_? ",
            NationalIdentificationNumber = "01234567", PhoneNumber = "+598 99 123 456", DateOfBirth = new DateOnly(2000, 1, 1)
        });
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        await _factory.DisposeAsync();
    }

    // //////////////////////////////////////////
    // Helpers
    // //////////////////////////////////////////
    private async Task<AuthenticationResponse> LoginAsync(string email = " ANA@EXAMPLE.COM ", string password = " Ab1!_? ")
    {
        using var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tokens = await response.Content.ReadFromJsonAsync<AuthenticationResponse>();
        Assert.NotNull(tokens);
        return tokens;
    }

    private Task<HttpResponseMessage> RefreshAsync(string token)
    {
        return _client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest { RefreshToken = token });
    }

    private async Task<HttpResponseMessage> SendAuthenticatedAsync(HttpMethod method, string url, string accessToken)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }

    private async Task<User> CreateStaffAsync(StaffRole role)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            FirstName = "Staff", LastName = "Test", Email = "staff@example.com", PasswordHash = string.Empty,
            ProfileType = ProfileType.Staff, AccountStatus = AccountStatus.Active, CreatedAt = now, UpdatedAt = now
        };
        user.PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>().HashPassword(user, " Ab1!_? ");
        db.Users.Add(user);
        db.EmployeeNumbers.Add(new EmployeeNumberRegistration { UserId = user.Id, EmployeeNumber = "EMP-000001" });
        db.Staff.Add(new Staff { UserId = user.Id, EmployeeNumber = "EMP-000001", StaffRole = role });
        await db.SaveChangesAsync();
        return user;
    }

    private string AddStalePermissions(string accessToken, string role)
    {
        var original = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        using var scope = _factory.Services.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<IOptions<JwtSettings>>().Value;
        var claims = new[]
        {
            new Claim("sub", original.Subject),
            new Claim("sid", original.Claims.Single(claim => claim.Type == "sid").Value),
            new Claim("account_status", "Active"),
            new Claim(ClaimTypes.Role, role)
        };
        var jwt = new JwtSecurityToken(settings.Issuer, settings.Audience, claims,
            DateTime.UtcNow.AddSeconds(-1), DateTime.UtcNow.AddMinutes(10),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    // //////////////////////////////////////////
    // Login and JWT validation
    // //////////////////////////////////////////
    [Fact]
    public async Task Login_IssuesLimitedJwtAndStoresOnlyRefreshHash()
    {
        var tokens = await LoginAsync();
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(tokens.AccessToken);
        Assert.Equal(SecurityAlgorithms.HmacSha256, jwt.Header.Alg);
        Assert.True(jwt.ValidTo - jwt.ValidFrom <= TimeSpan.FromMinutes(15));
        Assert.Contains(jwt.Claims, claim => claim.Type == JwtRegisteredClaimNames.Jti);
        Assert.Contains(jwt.Claims, claim => claim.Type == JwtRegisteredClaimNames.Iat);
        Assert.DoesNotContain(jwt.Claims, claim => claim.Type is "password" or "email" or "nationalIdentificationNumber" or "role");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        var session = await db.AuthenticationSessions.SingleAsync();
        var token = await db.RefreshTokens.SingleAsync();
        Assert.Equal(TimeSpan.FromDays(7), session.ExpiresAt - session.CreatedAt);
        Assert.Equal(session.Id.ToString(), jwt.Claims.Single(claim => claim.Type == "sid").Value);
        Assert.Equal(session.UserId.ToString(), jwt.Subject);
        Assert.Equal(SHA256.HashData(Encoding.UTF8.GetBytes(tokens.RefreshToken)), token.TokenHash);
        Assert.Equal(session.ExpiresAt, token.ExpiresAt);
        Assert.Null(token.ConsumedAt);
        using var response = await SendAuthenticatedAsync(HttpMethod.Get, "/api/auth/session", tokens.AccessToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("ana@example.com", "wrong123")]
    [InlineData("unknown@example.com", " Ab1!_? ")]
    public async Task Login_WithWrongCredentials_ReturnsUnauthorizedWithoutSession(string email, string password)
    {
        using var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = password });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<HospitalDbContext>().AuthenticationSessions.CountAsync());
    }

    [Theory]
    [InlineData("signature")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("expired")]
    [InlineData("ownership")]
    public async Task ProtectedRequest_WithInvalidJwt_ReturnsUnauthorized(string failure)
    {
        var tokens = await LoginAsync();
        var original = new JwtSecurityTokenHandler().ReadJwtToken(tokens.AccessToken);
        using var scope = _factory.Services.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<IOptions<JwtSettings>>().Value;
        var key = failure == "signature" ? Convert.ToHexString(RandomNumberGenerator.GetBytes(32)) : settings.SigningKey;
        var now = DateTime.UtcNow;
        var jwt = new JwtSecurityToken(
            issuer: failure == "issuer" ? "wrong-issuer" : settings.Issuer,
            audience: failure == "audience" ? "wrong-audience" : settings.Audience,
            claims: [new Claim("sub", failure == "ownership" ? Guid.NewGuid().ToString() : original.Subject), new Claim("sid", original.Claims.Single(claim => claim.Type == "sid").Value)],
            notBefore: now.AddMinutes(-20),
            expires: failure == "expired" ? now.AddMinutes(-1) : now.AddMinutes(10),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));
        using var response = await SendAuthenticatedAsync(HttpMethod.Get, "/api/auth/session", new JwtSecurityTokenHandler().WriteToken(jwt));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // //////////////////////////////////////////
    // Refresh rotation and reuse
    // //////////////////////////////////////////
    [Fact]
    public async Task Refresh_RotatesTokenWithoutExtendingSession()
    {
        var original = await LoginAsync();
        using var response = await RefreshAsync(original.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var replacement = await response.Content.ReadFromJsonAsync<AuthenticationResponse>();
        Assert.NotNull(replacement);
        Assert.NotEqual(original.RefreshToken, replacement.RefreshToken);
        Assert.True((replacement.SessionExpiresAt - original.SessionExpiresAt).Duration() < TimeSpan.FromMilliseconds(1));
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        var tokens = await db.RefreshTokens.ToListAsync();
        Assert.Equal(2, tokens.Count);
        Assert.Single(tokens, token => token.ConsumedAt == null);
        Assert.Single(tokens, token => token.ConsumedAt != null);
    }

    [Fact]
    public async Task Refresh_WithConsumedToken_RevokesOnlyItsSession()
    {
        var original = await LoginAsync();
        var independent = await LoginAsync();
        using var rotated = await RefreshAsync(original.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        var replacement = await rotated.Content.ReadFromJsonAsync<AuthenticationResponse>();
        Assert.NotNull(replacement);
        using var reused = await RefreshAsync(original.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
        using var denied = await SendAuthenticatedAsync(HttpMethod.Get, "/api/auth/session", replacement.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        using var deniedRefresh = await RefreshAsync(replacement.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, deniedRefresh.StatusCode);
        using var unaffected = await SendAuthenticatedAsync(HttpMethod.Get, "/api/auth/session", independent.AccessToken);
        Assert.Equal(HttpStatusCode.OK, unaffected.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithUnknownToken_DoesNotRevokeExistingSession()
    {
        var tokens = await LoginAsync();
        using var unknown = await RefreshAsync(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        using var session = await SendAuthenticatedAsync(HttpMethod.Get, "/api/auth/session", tokens.AccessToken);
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithConcurrentReuse_RevokesSession()
    {
        var original = await LoginAsync();
        var responses = await Task.WhenAll(RefreshAsync(original.RefreshToken), RefreshAsync(original.RefreshToken));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Unauthorized);
            var successful = responses.Single(response => response.StatusCode == HttpStatusCode.OK);
            var replacement = await successful.Content.ReadFromJsonAsync<AuthenticationResponse>();
            Assert.NotNull(replacement);
            using var denied = await SendAuthenticatedAsync(HttpMethod.Get, "/api/auth/session", replacement.AccessToken);
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        }
        finally
        {
            foreach (var response in responses)
                response.Dispose();
        }
    }

    // //////////////////////////////////////////
    // Logout and current permissions
    // //////////////////////////////////////////
    [Fact]
    public async Task Logout_RevokesCurrentSessionWithoutAffectingOtherLogins()
    {
        var first = await LoginAsync();
        var second = await LoginAsync();
        using var logout = await SendAuthenticatedAsync(HttpMethod.Post, "/api/auth/logout", first.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        using var denied = await SendAuthenticatedAsync(HttpMethod.Get, "/api/auth/session", first.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        using var deniedRefresh = await RefreshAsync(first.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, deniedRefresh.StatusCode);
        using var unaffected = await SendAuthenticatedAsync(HttpMethod.Get, "/api/auth/session", second.AccessToken);
        Assert.Equal(HttpStatusCode.OK, unaffected.StatusCode);
    }

    [Theory]
    [InlineData(AccountStatus.Suspended)]
    [InlineData(AccountStatus.Deactivated)]
    public async Task BlockedAccount_CannotLoginRefreshOrUseExistingJwt(AccountStatus status)
    {
        var tokens = await LoginAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
            var user = await db.Users.SingleAsync(user => user.Email == "ana@example.com");
            user.AccountStatus = status;
            await db.SaveChangesAsync();
        }
        using var login = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = "ana@example.com", Password = " Ab1!_? " });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        using var refresh = await RefreshAsync(tokens.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        using var session = await SendAuthenticatedAsync(HttpMethod.Get, "/api/auth/session", tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, session.StatusCode);
    }

    [Fact]
    public async Task ExpiredSession_RejectsAccessAndRefresh()
    {
        var tokens = await LoginAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
            var session = await db.AuthenticationSessions.SingleAsync();
            session.CreatedAt = DateTimeOffset.UtcNow.AddDays(-8);
            session.ExpiresAt = session.CreatedAt.AddDays(7);
            await db.SaveChangesAsync();
        }
        using var sessionResponse = await SendAuthenticatedAsync(HttpMethod.Get, "/api/auth/session", tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, sessionResponse.StatusCode);
        using var refresh = await RefreshAsync(tokens.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task PendingPatient_GainsActiveAccessAfterVerificationWithoutNewLogin()
    {
        var tokens = await LoginAsync();
        var staleToken = AddStalePermissions(tokens.AccessToken, "Patient");
        using var denied = await SendAuthenticatedAsync(HttpMethod.Get, "/__tests/authorization/active", staleToken);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var receptionist = await CreateStaffAsync(StaffRole.Receptionist);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
            var user = await db.Users.SingleAsync(user => user.Email == "ana@example.com");
            var patient = await db.Patients.SingleAsync(patient => patient.UserId == user.Id);
            patient.IdentityVerifiedAt = DateTimeOffset.UtcNow;
            patient.IdentityVerifiedByUserId = receptionist.Id;
            user.AccountStatus = AccountStatus.Active;
            await db.SaveChangesAsync();
        }
        using var allowed = await SendAuthenticatedAsync(HttpMethod.Get, "/__tests/authorization/active", staleToken);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task StaffPermissions_UseCurrentRoleInsteadOfJwtSnapshot()
    {
        var staffUser = await CreateStaffAsync(StaffRole.Administrator);
        var tokens = await LoginAsync("staff@example.com");
        var staleToken = AddStalePermissions(tokens.AccessToken, "Administrator");
        using var allowed = await SendAuthenticatedAsync(HttpMethod.Get, "/__tests/authorization/administrator", staleToken);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
            var staff = await db.Staff.SingleAsync(staff => staff.UserId == staffUser.Id);
            staff.StaffRole = StaffRole.Receptionist;
            await db.SaveChangesAsync();
        }
        using var denied = await SendAuthenticatedAsync(HttpMethod.Get, "/__tests/authorization/administrator", staleToken);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }
}
