using Microsoft.EntityFrameworkCore;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;

namespace SecureLab.Api.Scaffolding;

// Навчальний старт ЛР 02. Запускати лише з локальними штучними даними.
public static class Lab02Endpoints
{
    
public static void MapLab02Endpoints(this WebApplication app)
{
    app.MapGet("/api/incidents/search", async (
        string? q,
        string? sortBy,
        SecureLabDbContext db,
        CancellationToken ct) =>
    {
        if (!string.IsNullOrEmpty(sortBy) &&
    sortBy != "createdAtUtc" &&
    sortBy != "severity" &&
    sortBy != "status")
{
    return Results.ValidationProblem(new Dictionary<string, string[]>
    {
        ["sortBy"] = ["Допустимі значення: createdAtUtc, severity, status."]
    });
}

var searchTerm = $"%{q ?? ""}%";

var query = db.Incidents
    .AsNoTracking()
    .Where(i =>
        EF.Functions.ILike(i.Title, searchTerm) ||
        EF.Functions.ILike(i.Description, searchTerm));

query = sortBy switch
{
    "severity" => query.OrderBy(i => i.Severity),
    "status" => query.OrderBy(i => i.Status),
    _ => query.OrderByDescending(i => i.CreatedAtUtc)
};

var rows = await query
    .Take(50)
    .ToListAsync(ct);

        return Results.Ok(rows.Select(row => new
        {
            row.Id,
            row.Title,
            row.Description,
            Severity = row.Severity.ToString(),
            Status = row.Status.ToString(),
            row.CreatedAtUtc
        }));
    });

    app.MapPost("/api/incidents", async (
    CreateIncidentRequest request,
    SecureLabDbContext db,
    CancellationToken ct) =>
{
    var errors = new Dictionary<string, string[]>();

    if (string.IsNullOrWhiteSpace(request.Title))
        errors["title"] = ["Назва інциденту є обов'язковою."];
    else if (request.Title.Trim().Length > 160)
        errors["title"] = ["Назва не може перевищувати 160 символів."];

    if (string.IsNullOrWhiteSpace(request.Description))
        errors["description"] = ["Опис інциденту є обов'язковим."];
    else if (request.Description.Trim().Length > 4000)
        errors["description"] = ["Опис не може перевищувати 4000 символів."];

    if (!Enum.TryParse<IncidentSeverity>(
            request.Severity, true, out var severity) ||
        !Enum.IsDefined(severity))
    {
        errors["severity"] = ["Допустимі значення: Low, Medium, High, Critical."];
    }

    if (request.OccurredAtUtc is null)
    {
        errors["occurredAtUtc"] = ["Дата виникнення інциденту є обов'язковою."];
    }
    else if (request.OccurredAtUtc.Value > DateTimeOffset.UtcNow.AddMinutes(5))
    {
        errors["occurredAtUtc"] = ["Дата не може бути більш ніж на 5 хвилин у майбутньому."];
    }

    if (errors.Count > 0)
        return Results.ValidationProblem(errors);

    var title = request.Title!.Trim();
    var description = request.Description!.Trim();

    if ((severity == IncidentSeverity.High ||
         severity == IncidentSeverity.Critical) &&
        description.Length < 40)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["description"] = ["Для рівня High або Critical опис має містити щонайменше 40 символів."]
        });
    }

    var duplicateExists = await db.Incidents.AnyAsync(
    i => i.Title == title &&
         (i.Status == IncidentStatus.New ||
          i.Status == IncidentStatus.Triaged ||
          i.Status == IncidentStatus.InProgress ||
          i.Status == IncidentStatus.Resolved),
    ct);

if (duplicateExists)
{
    return Results.Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "Конфлікт даних",
        detail: "Активний інцидент із такою назвою вже існує.");
}

    var now = DateTimeOffset.UtcNow;

    var incident = new Incident
    {
        Id = Guid.NewGuid(),
        OwnerUserId = DbSeeder.AliceId,
        Title = title,
        Description = description,
        Severity = severity,
        Status = IncidentStatus.New,
        OccurredAtUtc = request.OccurredAtUtc!.Value.ToUniversalTime(),
        CreatedAtUtc = now,
        UpdatedAtUtc = now
    };

    db.Incidents.Add(incident);
    await db.SaveChangesAsync(ct);

    return Results.Created(
    $"/api/incidents/{incident.Id}",
    new CreatedIncidentResponse(incident.Id, incident.Title));
});
}

}

public sealed record CreateIncidentRequest(
    string? Title, string? Description, string? Severity, DateTimeOffset? OccurredAtUtc);
public sealed record CreatedIncidentResponse(Guid Id, string Title);