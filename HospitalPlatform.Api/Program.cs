using System.Text;
using HospitalPlatform.Api.Features.Authentication.Services;
using HospitalPlatform.Api.Features.Authentication.Settings;
using HospitalPlatform.Api.Features.Doctors.Services;
using HospitalPlatform.Api.Features.Patients.Services;
using HospitalPlatform.Api.Features.Staff.Services;
using HospitalPlatform.Api.Features.Users.Models;
using HospitalPlatform.Api.Features.Users.Services;
using HospitalPlatform.Api.Infrastructure.Middleware;
using HospitalPlatform.Api.Infrastructure.Persistence;
using HospitalPlatform.Api.Infrastructure.Persistence.Seeds;
using HospitalPlatform.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

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
builder.Services.AddScoped<IDoctorRegistrationService, DoctorRegistrationService>();
builder.Services.AddScoped<IPatientRegistrationService, PatientRegistrationService>();
builder.Services.AddScoped<IStaffRegistrationService, StaffRegistrationService>();
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
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[JwtBearerDefaults.AuthenticationScheme] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT"
        };
        return Task.CompletedTask;
    });
    options.AddOperationTransformer((operation, context, _) =>
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        if (metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any())
        {
            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(JwtBearerDefaults.AuthenticationScheme, context.Document)] = []
            });
        }
        return Task.CompletedTask;
    });
});

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
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Hospital Platform API v1"));
}

// //////////////////////////////////////////
// Database seed
// //////////////////////////////////////////
if (app.Configuration.GetValue<bool>("seed"))
{
    var password = app.Configuration["DatabaseSeed:Administrator:Password"]
        ?? throw new InvalidOperationException("Configure DatabaseSeed:Administrator:Password before running the database seed.");
    await using (var scope = app.Services.CreateAsyncScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        await db.Database.MigrateAsync();
        await DatabaseSeed.SeedAsync(db, passwordHasher, password);
    }
    app.Logger.LogInformation("Database seed completed.");
    await app.DisposeAsync();
    return;
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
