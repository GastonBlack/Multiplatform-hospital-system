using HospitalPlatform.Api.Features.Patients.Services;
using HospitalPlatform.Api.Features.Users.Models;
using HospitalPlatform.Api.Infrastructure.Middleware;
using HospitalPlatform.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddScoped<IPatientRegistrationService, PatientRegistrationService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddDbContext<HospitalDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("HospitalDatabase")
        ?? throw new InvalidOperationException("ConnectionStrings:HospitalDatabase must be configured.");

    options.UseNpgsql(connectionString);
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}


app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
