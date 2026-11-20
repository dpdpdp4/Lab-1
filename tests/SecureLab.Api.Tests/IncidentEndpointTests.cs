using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SecureLab.Api.Data;
using SecureLab.Api.Presentation.Contracts;

namespace SecureLab.Api.Tests;

public sealed class IncidentEndpointTests(SecureLabApiFactory factory)
    : IClassFixture<SecureLabApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

        [Fact]
    public async Task GetList_ReturnsOnlyCurrentReporterIncidents()
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

        using var client = await factory.LoginAsync("alice", password);

        var incidents = await client.GetFromJsonAsync<List<IncidentListItemResponse>>(
            "/api/incidents");

        Assert.NotNull(incidents);
        Assert.NotEmpty(incidents);
        Assert.All(incidents, incident =>
            Assert.NotEqual(DbSeeder.BobIncidentId, incident.Id));
        Assert.Contains(incidents, incident =>
            incident.Id == DbSeeder.AliceIncidentId);
    }

        [Fact]
    public async Task GetDetails_ForAnotherUsersIncident_Returns404()
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

        using var client = await factory.LoginAsync("alice", password);

        using var response = await client.GetAsync(
            $"/api/incidents/{DbSeeder.BobIncidentId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

        [Fact]
    public async Task GetDetails_AnalystCanReadIncidentAndInternalComment()
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

        using var client = await factory.LoginAsync("morgan", password);

        using var response = await client.GetAsync(
            $"/api/incidents/{DbSeeder.AliceIncidentId}");

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(document.RootElement.TryGetProperty("ownerUserId", out _));
        Assert.False(document.RootElement.TryGetProperty("email", out _));
        Assert.Equal(
            "Аліса Коваль",
            document.RootElement.GetProperty("ownerDisplayName").GetString());

        var comments = document.RootElement.GetProperty("comments");

        Assert.Contains(
            comments.EnumerateArray(),
            comment => comment.GetProperty("text").GetString()!
                .Contains("Внутрішня примітка", StringComparison.Ordinal));
    }

        [Fact]
    public async Task GetList_WithoutLogin_ReturnsUnauthorized()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/incidents");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

        [Fact]
    public async Task CreateIncident_Analyst_ReturnsForbidden()
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

        using var client = await factory.LoginAsync("morgan", password);

        using var response = await client.PostAsJsonAsync("/api/incidents", new
        {
            title = "Analyst create test",
            description = "Analyst should not be allowed to create incidents.",
            severity = "Low",
            occurredAtUtc = DateTimeOffset.UtcNow
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

        [Fact]
    public async Task UpdateIncident_CannotUpdateAnotherUsersIncident()
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

        using var client = await factory.LoginAsync("alice", password);

        using var response = await client.PutAsJsonAsync(
            $"/api/incidents/{DbSeeder.BobIncidentId}",
            new
            {
                title = "Спроба змінити чужий інцидент",
                description = "Ця зміна не повинна бути збережена."
            });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
public async Task TriageIncident_AnalystCanTriageNewIncident()
{
    var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
        ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

    using var reporter = await factory.LoginAsync("alice", password);

    var createResponse = await reporter.PostAsJsonAsync(
        "/api/incidents",
        new
        {
            title = $"Тест переходу статусу {Guid.NewGuid()}",
            description = "Новий тестовий інцидент для перевірки роботи аналітика.",
            severity = "Low",
            occurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10)
        });

    Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

    var created = await createResponse.Content
        .ReadFromJsonAsync<JsonElement>();

    var incidentId = created.GetProperty("id").GetGuid();

    using var analyst = await factory.LoginAsync("morgan", password);

    using var triageResponse = await analyst.PostAsync(
        $"/api/incidents/{incidentId}/triage",
        null);

    Assert.Equal(HttpStatusCode.NoContent, triageResponse.StatusCode);

    using var deleteResponse = await reporter.DeleteAsync(
    $"/api/incidents/{incidentId}");

    Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
}

        [Fact]
    public async Task TriageIncident_Reporter_ReturnsForbidden()
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

        using var client = await factory.LoginAsync("alice", password);

        using var response = await client.PostAsync(
            $"/api/incidents/{DbSeeder.AliceIncidentId}/triage",
            null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

        [Fact]
    public async Task DeleteIncident_CannotDeleteAnotherUsersIncident()
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

        using var client = await factory.LoginAsync("alice", password);

        using var response = await client.DeleteAsync(
            $"/api/incidents/{DbSeeder.BobIncidentId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

        [Fact]
    public async Task GetComment_ReporterCannotReadInternalComment()
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

        using var client = await factory.LoginAsync("alice", password);

        var internalCommentId = Guid.Parse(
            "30000000-0000-0000-0000-000000000003");

        using var response = await client.GetAsync(
            $"/api/incident-comments/{internalCommentId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

        [Fact]
    public async Task GetComment_AnalystCanReadInternalComment()
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

        using var client = await factory.LoginAsync("morgan", password);

        var internalCommentId = Guid.Parse(
            "30000000-0000-0000-0000-000000000003");

        using var response = await client.GetAsync(
            $"/api/incident-comments/{internalCommentId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateIncident_CanUpdateOwnIncident()
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

        using var client = await factory.LoginAsync("alice", password);

        using var response = await client.PutAsJsonAsync(
            $"/api/incidents/{DbSeeder.AliceIncidentId}",
            new
            {
                title = "Оновлений власний інцидент",
                description = "Перевірка дозволеного редагування власного інциденту."
            });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

        [Fact]
    public async Task DeleteIncident_CanDeleteOwnIncident()
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

        using var client = await factory.LoginAsync("alice", password);

        using var createResponse = await client.PostAsJsonAsync("/api/incidents", new
        {
            title = "Тест видалення " + Guid.NewGuid().ToString("N"),
            description = "Тимчасовий інцидент для перевірки видалення власного ресурсу.",
            severity = "Low",
            occurredAtUtc = DateTimeOffset.UtcNow
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var incidentId = created.GetProperty("id").GetGuid();

        using var deleteResponse = await client.DeleteAsync(
            $"/api/incidents/{incidentId}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }


    [Fact]
    public async Task ClientScript_DoesNotUseDangerousInnerHtmlSink()
    {
        var script = await _client.GetStringAsync("/app.js");

        Assert.DoesNotContain("innerHTML", script, StringComparison.Ordinal);
        Assert.Contains("textContent", script, StringComparison.Ordinal);
    }
        [Fact]
    public async Task GetSeveritySummary_WithoutStatus_ReturnsAllSeverityGroups()
    {
        var summary = await _client.GetFromJsonAsync<List<IncidentSeveritySummaryResponse>>(
            "/api/incidents/severity-summary");

        Assert.NotNull(summary);
        Assert.Equal(3, summary.Count);
        Assert.Equal(1, summary.Single(item => item.Severity == "High").Count);
        Assert.Equal(3, summary.Single(item => item.Severity == "Low").Count);
        Assert.Equal(1, summary.Single(item => item.Severity == "Medium").Count);

        Assert.Equal(
            new[] { "High", "Low", "Medium" },
            summary.Select(item => item.Severity).OrderBy(severity => severity));
    }

    [Fact]
    public async Task GetSeveritySummary_WithTriagedStatus_ReturnsMediumGroup()
    {
        var summary = await _client.GetFromJsonAsync<List<IncidentSeveritySummaryResponse>>(
            "/api/incidents/severity-summary?status=Triaged");

        Assert.NotNull(summary);
        var item = Assert.Single(summary);

        Assert.Equal("Medium", item.Severity);
        Assert.Equal(1, item.Count);
    }

    [Fact]
    public async Task GetSeveritySummary_WithResolvedStatus_ReturnsEmptyArray()
    {
        var summary = await _client.GetFromJsonAsync<List<IncidentSeveritySummaryResponse>>(
            "/api/incidents/severity-summary?status=Resolved");

        Assert.NotNull(summary);
        Assert.Empty(summary);
    }

    [Fact]
    public async Task GetSeveritySummary_WithUnknownStatus_ReturnsBadRequest()
    {
        using var response = await _client.GetAsync(
            "/api/incidents/severity-summary?status=Unknown");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
