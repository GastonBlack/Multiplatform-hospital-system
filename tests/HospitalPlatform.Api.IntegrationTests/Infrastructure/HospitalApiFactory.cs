using System.Security.Cryptography;
using HospitalPlatform.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace HospitalPlatform.Api.IntegrationTests.Infrastructure;

public class HospitalApiFactory : WebApplicationFactory<Program>
{
    private readonly string databaseName = "hospital_test_" + Guid.NewGuid().ToString("N");
    private NpgsqlConnection? adminConnection;
    private string testConnectionString = string.Empty;
    private readonly string signingKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    // Creates an isolated database before starting the API and applying migrations.
    public async Task InitializeAsync()
    {
        var configuration = new ConfigurationBuilder().AddUserSecrets<HospitalDbContext>().Build();
        var connectionString = Environment.GetEnvironmentVariable("HOSPITAL_TEST_CONNECTION_STRING")
            ?? configuration.GetConnectionString("HospitalDatabase")
            ?? throw new InvalidOperationException("Configure HOSPITAL_TEST_CONNECTION_STRING or the API's development connection secret.");

        var adminOptions = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = "postgres",
            Pooling = false
        };
        adminConnection = new NpgsqlConnection(adminOptions.ConnectionString);
        await adminConnection.OpenAsync();
        await using var create = new NpgsqlCommand($"CREATE DATABASE {databaseName}", adminConnection);
        await create.ExecuteNonQueryAsync();

        adminOptions.Database = databaseName;
        testConnectionString = adminOptions.ConnectionString;
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services => services.AddControllers().AddApplicationPart(typeof(AuthorizationProbeController).Assembly));
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:HospitalDatabase"] = testConnectionString,
                ["Authentication:SigningKey"] = signingKey
            }));
    }

    // Stops the API before removing only this factory's generated database.
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (adminConnection is not null)
        {
            if (adminConnection.State == System.Data.ConnectionState.Open)
            {
                await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {databaseName}", adminConnection);
                await drop.ExecuteNonQueryAsync();
            }
            await adminConnection.DisposeAsync();
        }
    }
}
