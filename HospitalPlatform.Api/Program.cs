using HospitalPlatform.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddDbContext<HospitalDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("HospitalDatabase")
        ?? throw new InvalidOperationException("ConnectionStrings:HospitalDatabase must be configured.");

    options.UseNpgsql(connectionString);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}


app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
