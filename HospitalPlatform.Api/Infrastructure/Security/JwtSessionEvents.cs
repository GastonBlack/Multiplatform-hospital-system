using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using HospitalPlatform.Api.Features.Staff.Models;
using HospitalPlatform.Api.Features.Users.Models;
using HospitalPlatform.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace HospitalPlatform.Api.Infrastructure.Security;

public class JwtSessionEvents : JwtBearerEvents
{
    // //////////////////////////////////////////
    // Dependencies
    // //////////////////////////////////////////
    private readonly HospitalDbContext _db;

    public JwtSessionEvents(HospitalDbContext db)
    {
        _db = db;
    }

    // //////////////////////////////////////////
    // Current session and account validation
    // //////////////////////////////////////////
    public override async Task TokenValidated(TokenValidatedContext context)
    {
        if (!Guid.TryParse(context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId) ||
            !Guid.TryParse(context.Principal?.FindFirstValue("sid"), out var sessionId))
        {
            context.Fail("Token identity is invalid.");
            return;
        }

        var session = await _db.AuthenticationSessions.AsNoTracking().SingleOrDefaultAsync(session => session.Id == sessionId);
        if (session == null || session.UserId != userId || session.RevokedAt != null || session.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            context.Fail("Session is no longer valid.");
            return;
        }

        var user = await _db.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Id == userId);
        if (user == null || (user.AccountStatus != AccountStatus.Active &&
            !(user.AccountStatus == AccountStatus.PendingVerification && user.ProfileType == ProfileType.Patient)))
        {
            context.Fail("Account cannot authenticate.");
            return;
        }

        var role = user.ProfileType.ToString();
        if (user.ProfileType == ProfileType.Staff)
        {
            var staffRole = await _db.Staff.Where(staff => staff.UserId == userId)
                .Select(staff => (StaffRole?)staff.StaffRole).SingleOrDefaultAsync();
            if (staffRole == null)
            {
                context.Fail("Staff profile is missing.");
                return;
            }
            role = staffRole.Value.ToString();
        }

        // Permissions come from current database values, not a JWT role snapshot.
        var identity = (ClaimsIdentity)context.Principal!.Identity!;
        var stalePermissions = identity.Claims.Where(claim =>
            claim.Type == "profile_type" || claim.Type == "account_status" || claim.Type == ClaimTypes.Role).ToList();
        foreach (var claim in stalePermissions)
            identity.RemoveClaim(claim);

        identity.AddClaim(new Claim("profile_type", user.ProfileType.ToString()));
        identity.AddClaim(new Claim("account_status", user.AccountStatus.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Role, role));
    }
}
