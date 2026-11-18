using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecureLab.Api.Data;

namespace SecureLab.Api.Tests;

public sealed class AuthenticationMechanicsTests(SecureLabApiFactory factory)
    : IClassFixture<SecureLabApiFactory>
{
    [Fact]
    public async Task RealCookie_ReachesSessionCheck()
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

        using var client = await factory.LoginAsync("alice", password);
        using var response = await client.GetAsync("/api/auth/session-check");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithValidData_ReturnsCreated()
    {
        using var client = factory.CreateClient();

        var suffix = Guid.NewGuid().ToString("N")[..20];

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            userName = "lab" + suffix,
            email = suffix + "@example.test",
            displayName = "Test User",
            password = "TestPass2026!"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithInvalidEmail_ReturnsBadRequest()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            userName = "lab" + Guid.NewGuid().ToString("N"),
            email = "invalid-email",
            displayName = "Test User",
            password = "TestPass2026!"
        });

        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsBadRequest()
    {
        using var client = factory.CreateClient();

        var suffix = Guid.NewGuid().ToString("N")[..20];
        var email = suffix + "@example.test";

        var input = new
        {
            userName = "lab" + suffix,
            email,
            displayName = "Test User",
            password = "TestPass2026!"
        };

        using var firstResponse =
            await client.PostAsJsonAsync("/api/auth/register", input);

        using var secondResponse =
            await client.PostAsJsonAsync("/api/auth/register", new
            {
                userName = "other" + suffix,
                email,
                displayName = "Another User",
                password = "TestPass2026!"
            });

        var firstBody = await firstResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, secondResponse.StatusCode);
    }

    [Fact]
public async Task Logout_EndsSession()
{
    var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
        ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

    using var client = await factory.LoginAsync("alice", password);

    using var logoutResponse = await client.PostAsync(
        "/api/auth/logout",
        null);

    Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

    using var meResponse = await client.GetAsync("/api/me");

    Assert.Equal(HttpStatusCode.Unauthorized, meResponse.StatusCode);
}

    [Fact]
public async Task Me_WithLogin_ReturnsAuthenticatedUser()
{
    var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
        ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

    using var client = await factory.LoginAsync("alice", password);
    using var response = await client.GetAsync("/api/me");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var body = await response.Content.ReadFromJsonAsync<JsonElement>();

    Assert.Equal("alice", body.GetProperty("userName").GetString());
}

[Fact]
public async Task Login_WithUnknownUser_ReturnsUnauthorized()
{
    using var client = factory.CreateClient();

    using var response = await client.PostAsJsonAsync(
        "/api/auth/login",
        new
        {
            userName = "unknown-user-123",
            password = "WrongPass2026!"
        });

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
}

[Fact]
public async Task Login_WithWrongPassword_ReturnsUnauthorized()
{
    using var client = factory.CreateClient();

    using var response = await client.PostAsJsonAsync(
        "/api/auth/login",
        new
        {
            userName = "alice",
            password = "WrongPass2026!"
        });

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
}

[Fact]
public async Task Me_IgnoresForgedDemoUserIdHeader()
{
    var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
        ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

    using var client = await factory.LoginAsync("alice", password);

    client.DefaultRequestHeaders.Add(
        "X-Demo-UserId",
        Guid.NewGuid().ToString());

    using var response = await client.GetAsync("/api/me");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var body = await response.Content.ReadFromJsonAsync<JsonElement>();

    Assert.Equal("alice", body.GetProperty("userName").GetString());
}

    [Fact]
public async Task Me_WithoutLogin_ReturnsUnauthorized()
{
    using var client = factory.CreateClient();

    using var response = await client.GetAsync("/api/me");

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
}

    [Fact]
    public async Task CreateIncident_IgnoresForgedOwnerUserId()
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

        using var client = await factory.LoginAsync("bob", password);

        var incidentId = Guid.Empty;

        try
        {
            using var response = await client.PostAsJsonAsync("/api/incidents", new
            {
                title = "Auth owner test " + Guid.NewGuid().ToString("N"),
                description = "This is a sufficiently detailed description for the authentication owner test.",
                severity = "Low",
                occurredAtUtc = DateTimeOffset.UtcNow,
                ownerUserId = DbSeeder.AliceId
            });

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            incidentId = body.GetProperty("id").GetGuid();

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SecureLabDbContext>();

            var savedIncident = await db.Incidents
                .AsNoTracking()
                .SingleAsync(i => i.Id == incidentId);

            Assert.Equal(DbSeeder.BobId, savedIncident.OwnerUserId);
            Assert.NotEqual(DbSeeder.AliceId, savedIncident.OwnerUserId);
        }
        finally
        {
            if (incidentId != Guid.Empty)
            {
                using var scope = factory.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SecureLabDbContext>();

                await db.Incidents
                    .Where(i => i.Id == incidentId)
                    .ExecuteDeleteAsync();
            }
        }
    }
        [Fact]
    public async Task CreateIncident_WithoutLogin_ReturnsUnauthorized()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/incidents", new
        {
            title = "Unauthorized incident test",
            description = "This request should be rejected because the user is not logged in.",
            severity = "Low",
            occurredAtUtc = DateTimeOffset.UtcNow
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
