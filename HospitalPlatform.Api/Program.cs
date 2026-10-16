using System.Text;
using HospitalPlatform.Api.Features.Authentication.Services;
using HospitalPlatform.Api.Features.Authentication.Settings;
using HospitalPlatform.Api.Features.Patients.Services;
using HospitalPlatform.Api.Features.Users.Models;
using HospitalPlatform.Api.Features.Users.Services;
using HospitalPlatform.Api.Infrastructure.Middleware;
using HospitalPlatform.Api.Infrastructure.Persistence;
using HospitalPlatform.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// //////////////////////////////////////////
// Database
// //////////////////////////////////////////
builder.Services.AddDbContext<HospitalDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("HospitalDatabase")
        ?? throw new InvalidOperationException("ConnectionStrings:HospitalDatabase must be configured.");

    options.UseNpgsql(connectionString);
});

// //////////////////////////////////////////
// Feature Services
// //////////////////////////////////////////
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IPatientRegistrationService, PatientRegistrationService>();
builder.Services.AddScoped<IUserProfileService, UserProfileService>();

// //////////////////////////////////////////
// Authentication
// //////////////////////////////////////////
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<JwtSessionEvents>();
builder.Services.AddOptions<JwtSettings>()
    .BindConfiguration("Authentication")
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwt = builder.Configuration.GetSection("Authentication").Get<JwtSettings>()!;
        options.MapInboundClaims = false;
        options.EventsType = typeof(JwtSessionEvents);
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            NameClaimType = "sub"
        };
    });

// //////////////////////////////////////////
// Authorization
// //////////////////////////////////////////
builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireClaim("account_status", "Active")
        .Build();
    options.AddPolicy("AuthenticatedAccount", policy => policy.RequireAuthenticatedUser());
});

// //////////////////////////////////////////
// API Services
// //////////////////////////////////////////
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// //////////////////////////////////////////
// Error Handling
// //////////////////////////////////////////
app.UseMiddleware<ExceptionHandlingMiddleware>();

// //////////////////////////////////////////
// Development
// //////////////////////////////////////////
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// //////////////////////////////////////////
// Middleware
// //////////////////////////////////////////
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// //////////////////////////////////////////
// Endpoints
// //////////////////////////////////////////
app.MapControllers();

app.Run();

// Allows integration tests to start the application's real entry point.
public partial class Program { }
