using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HospitalPlatform.Api.Tests.Integration.Infrastructure;

public class OpenApiTests
{
    [Theory]
    [InlineData("Development", HttpStatusCode.OK)]
    [InlineData("Production", HttpStatusCode.NotFound)]
    [InlineData("Testing", HttpStatusCode.NotFound)]
    public async Task Documentation_IsAvailableOnlyInDevelopment(string environment, HttpStatusCode expectedStatus)
    {
        await using var factory = CreateFactory(environment);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var ui = await client.GetAsync("/swagger/index.html");
        using var document = await client.GetAsync("/openapi/v1.json");
        using var script = await client.GetAsync("/swagger/swagger-ui-bundle.js");
        using var initializer = await client.GetAsync("/swagger/index.js");
        Assert.Equal(expectedStatus, ui.StatusCode);
        Assert.Equal(expectedStatus, document.StatusCode);
        Assert.Equal(expectedStatus, script.StatusCode);
        Assert.Equal(expectedStatus, initializer.StatusCode);
        if (environment == "Development")
        {
            Assert.Contains("id=\"swagger-ui\"", await ui.Content.ReadAsStringAsync());
            Assert.Contains("/openapi/v1.json", await initializer.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task OpenApi_DescribesJwtSecurityOnlyForProtectedOperations()
    {
        await using var factory = CreateFactory("Development");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var bearer = json.RootElement.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        Assert.Equal("http", bearer.GetProperty("type").GetString());
        Assert.Equal("bearer", bearer.GetProperty("scheme").GetString());
        Assert.Equal("JWT", bearer.GetProperty("bearerFormat").GetString());
        var paths = json.RootElement.GetProperty("paths");
        foreach (var (path, method) in new[]
        {
            ("/api/auth/session", "get"), ("/api/auth/logout", "post"),
            ("/api/users/me", "get"), ("/api/patients/me", "get"), ("/api/staff/register", "post")
        })
        {
            var security = paths.GetProperty(path).GetProperty(method).GetProperty("security");
            Assert.Contains(security.EnumerateArray(), requirement => requirement.TryGetProperty("Bearer", out _));
        }
        foreach (var path in new[] { "/api/auth/login", "/api/auth/refresh", "/api/patients/register" })
        {
            var operation = paths.GetProperty(path).GetProperty("post");
            Assert.False(operation.TryGetProperty("security", out var security) && security.GetArrayLength() > 0);
        }
        Assert.True(paths.GetProperty("/api/staff/register").GetProperty("post").GetProperty("responses").TryGetProperty("201", out _));
        Assert.False(json.RootElement.TryGetProperty("security", out var globalSecurity) && globalSecurity.GetArrayLength() > 0);
    }

    private static WebApplicationFactory<Program> CreateFactory(string environment)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:HospitalDatabase"] = "Host=127.0.0.1;Port=1;Database=hospital_swagger_test;Username=unused;Password=unused;Timeout=1",
                    ["Authentication:SigningKey"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
                    ["seed"] = "false"
                }));
        });
    }
}
