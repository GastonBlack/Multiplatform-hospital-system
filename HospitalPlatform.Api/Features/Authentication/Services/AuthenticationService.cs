using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HospitalPlatform.Api.Features.Authentication.DTOs;
using HospitalPlatform.Api.Features.Authentication.Models;
using HospitalPlatform.Api.Features.Authentication.Settings;
using HospitalPlatform.Api.Features.Users.Models;
using HospitalPlatform.Api.Features.Users.Validation;
using HospitalPlatform.Api.Infrastructure.Middleware.Exceptions;
using HospitalPlatform.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HospitalPlatform.Api.Features.Authentication.Services;

public class AuthenticationService : IAuthenticationService
{
    private const int AccessTokenLifetimeMinutes = 15;
    private const int SessionLifetimeDays = 7;
    private const int RefreshTokenByteLength = 32;

    // //////////////////////////////////////////
    // Dependencies
    // //////////////////////////////////////////
    private readonly HospitalDbContext _db;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly JwtSettings _jwt;

    public AuthenticationService(HospitalDbContext db, IPasswordHasher<User> passwordHasher, IOptions<JwtSettings> jwt)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwt = jwt.Value;
    }

    // //////////////////////////////////////////
    // Helpers
    // //////////////////////////////////////////
    private static byte[] HashRefreshToken(string token)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(token));
    }

    private async Task<User> LockUserAsync(Guid userId)
    {
        var user = await _db.Users.FromSqlInterpolated(
            $"SELECT * FROM \"Users\" WHERE \"Id\" = {userId} FOR SHARE")
            .AsNoTracking().SingleOrDefaultAsync();

        if (user == null)
            throw new UnauthorizedException("Account cannot authenticate.");

        return user;
    }

    private async Task<AuthenticationSession> LockSessionAsync(Guid userId, Guid sessionId)
    {
        var session = await _db.AuthenticationSessions.FromSqlInterpolated(
            $"SELECT * FROM \"AuthenticationSessions\" WHERE \"Id\" = {sessionId} AND \"UserId\" = {userId} FOR UPDATE")
            .SingleOrDefaultAsync();

        if (session == null || session.RevokedAt != null || session.ExpiresAt <= DateTimeOffset.UtcNow)
            throw new UnauthorizedException("Session is no longer valid.");

        return session;
    }

    private AuthenticationResponse IssueTokens(User user, AuthenticationSession session)
    {
        var now = DateTimeOffset.UtcNow;
        if (session.ExpiresAt.ToUnixTimeSeconds() <= now.ToUnixTimeSeconds())
            throw new UnauthorizedException("Session has expired.");

        var accessExpiry = now.AddMinutes(AccessTokenLifetimeMinutes);
        if (accessExpiry > session.ExpiresAt)
            accessExpiry = session.ExpiresAt;

        var rawRefreshToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(RefreshTokenByteLength));
        _db.RefreshTokens.Add(new RefreshToken
        {
            SessionId = session.Id,
            TokenHash = HashRefreshToken(rawRefreshToken),
            CreatedAt = now,
            ExpiresAt = session.ExpiresAt
        });

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim("sid", session.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64)
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SigningKey));
        var jwt = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: accessExpiry.UtcDateTime,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new AuthenticationResponse
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(jwt),
            RefreshToken = rawRefreshToken,
            AccessTokenExpiresAt = accessExpiry,
            SessionExpiresAt = session.ExpiresAt
        };
    }

    // //////////////////////////////////////////
    // Login
    // //////////////////////////////////////////
    public async Task<AuthenticationResponse> LoginAsync(LoginRequest request)
    {
        Validator.ValidateObject(request, new ValidationContext(request), validateAllProperties: true);
        if (!EmailNormalizer.TryNormalize(request.Email, out var email))
            throw new BadRequestException("Email must be a valid email address.");

        await using var transaction = await _db.Database.BeginTransactionAsync();
        var user = await _db.Users.FromSqlInterpolated(
            $"SELECT * FROM \"Users\" WHERE \"Email\" = {email} FOR SHARE")
            .AsNoTracking().SingleOrDefaultAsync();

        if (user == null || _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
            throw new UnauthorizedException("Email or password is incorrect.");

        var now = DateTimeOffset.UtcNow;
        var session = new AuthenticationSession { UserId = user.Id, CreatedAt = now, ExpiresAt = now.AddDays(SessionLifetimeDays) };
        _db.AuthenticationSessions.Add(session);
        var response = IssueTokens(user, session);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        return response;
    }

    // //////////////////////////////////////////
    // Refresh
    // //////////////////////////////////////////
    public async Task<AuthenticationResponse> RefreshAsync(RefreshRequest request)
    {
        Validator.ValidateObject(request, new ValidationContext(request), validateAllProperties: true);
        var hash = HashRefreshToken(request.RefreshToken);
        var token = await _db.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(token => token.TokenHash.SequenceEqual(hash));
        if (token == null)
            throw new UnauthorizedException("Refresh token is invalid.");

        var userId = await _db.AuthenticationSessions.Where(session => session.Id == token.SessionId).Select(session => session.UserId).SingleAsync();
        await using var transaction = await _db.Database.BeginTransactionAsync();
        var user = await LockUserAsync(userId);
        var session = await LockSessionAsync(userId, token.SessionId);
        token = await _db.RefreshTokens.SingleAsync(current => current.Id == token.Id);

        // Reuse must commit revocation before returning the rejection.
        if (token.ConsumedAt != null)
        {
            session.RevokedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            throw new UnauthorizedException("Refresh token was already used. Please log in again.");
        }

        if (token.ExpiresAt <= DateTimeOffset.UtcNow)
            throw new UnauthorizedException("Refresh token has expired.");

        // Free the unique unconsumed-token slot before inserting its replacement.
        token.ConsumedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        var response = IssueTokens(user, session);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        return response;
    }

    // //////////////////////////////////////////
    // Logout
    // //////////////////////////////////////////
    public async Task LogoutAsync(Guid userId, Guid sessionId)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync();
        await LockUserAsync(userId);
        var session = await LockSessionAsync(userId, sessionId);
        session.RevokedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
