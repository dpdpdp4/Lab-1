using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using SecureLab.Api.Application.Incidents;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;
using SecureLab.Api.Presentation.Contracts;
using Microsoft.EntityFrameworkCore;

namespace SecureLab.Api.Presentation.Endpoints;

public static class IncidentEndpoints
{
    public static IEndpointRouteBuilder MapIncidentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/incidents")
            .WithTags("Incidents");

        group.MapGet("/", GetListAsync)
            .WithName("GetIncidents")
            .Produces<IReadOnlyList<IncidentListItemResponse>>()
            .ProducesValidationProblem()
            .RequireAuthorization();

        group.MapGet("/{id:guid}", GetDetailsAsync)
            .WithName("GetIncidentDetails")
            .Produces<IncidentDetailsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();

        group.MapPut("/{id:guid}", UpdateAsync)
            .WithName("UpdateIncident")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem()
            .RequireAuthorization();

        group.MapDelete("/{id:guid}", DeleteAsync)
            .WithName("DeleteIncident")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();

        group.MapPost("/{id:guid}/triage", TriageAsync)
            .WithName("TriageIncident")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization();

        group.MapGet("/severity-summary", GetSeveritySummaryAsync)
            .WithName("GetIncidentSeveritySummary")
            .Produces<IReadOnlyList<IncidentSeveritySummaryResponse>>()
            .ProducesValidationProblem();

        return endpoints;
    }

    private static async Task<IResult> GetListAsync(
        string? status,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        IncidentQueries queries,
        CancellationToken cancellationToken)
    {
        var currentUser = await userManager.GetUserAsync(principal);

        if (currentUser is null)
        {
            return Results.Unauthorized();
        }

        var isAnalyst = await userManager.IsInRoleAsync(
            currentUser,
            DbSeeder.AnalystRole);

        var isReporter = await userManager.IsInRoleAsync(
            currentUser,
            DbSeeder.ReporterRole);

        if (!isAnalyst && !isReporter)
        {
            return Results.Forbid();
        }

        IncidentStatus? parsedStatus = null;

        if (status is not null)
        {
            if (!Enum.TryParse<IncidentStatus>(status, ignoreCase: true, out var candidate)
                || !Enum.IsDefined(candidate))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["status"] = ["Допустимі значення: New, Triaged, InProgress, Resolved, Closed."]
                });
            }

            parsedStatus = candidate;
        }

        var incidents = await queries.GetListAsync(
            parsedStatus,
            currentUser.Id,
            isAnalyst,
            cancellationToken);

        return Results.Ok(incidents);
    }

    private static async Task<IResult> GetDetailsAsync(
        Guid id,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        IncidentQueries queries,
        CancellationToken cancellationToken)
    {
        var currentUser = await userManager.GetUserAsync(principal);

        if (currentUser is null)
        {
            return Results.Unauthorized();
        }

        var isAnalyst = await userManager.IsInRoleAsync(
            currentUser,
            DbSeeder.AnalystRole);

        var isReporter = await userManager.IsInRoleAsync(
            currentUser,
            DbSeeder.ReporterRole);

        if (!isAnalyst && !isReporter)
        {
            return Results.Forbid();
        }

        var incident = await queries.GetDetailsAsync(
            id,
            currentUser.Id,
            isAnalyst,
            cancellationToken);

        return incident is null
            ? Results.Problem(
                title: "Інцидент не знайдено",
                detail: "Інцидент не існує або у вас немає прав на його перегляд.",
                statusCode: StatusCodes.Status404NotFound)
            : Results.Ok(incident);
    }

        private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateIncidentRequest request,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        SecureLabDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var currentUser = await userManager.GetUserAsync(principal);

        if (currentUser is null)
        {
            return Results.Unauthorized();
        }

        var isReporter = await userManager.IsInRoleAsync(
            currentUser,
            DbSeeder.ReporterRole);

        if (!isReporter)
        {
            return Results.Forbid();
        }

        var incident = await dbContext.Incidents.SingleOrDefaultAsync(
            item => item.Id == id && item.OwnerUserId == currentUser.Id,
            cancellationToken);

        if (incident is null)
        {
            return Results.Problem(
                title: "Інцидент не знайдено",
                detail: "Інцидент не існує або у вас немає прав на його редагування.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            errors["title"] = ["Вкажіть назву інциденту."];
        }
        else if (request.Title.Length > 160)
        {
            errors["title"] = ["Назва не повинна перевищувати 160 символів."];
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            errors["description"] = ["Вкажіть опис інциденту."];
        }
        else if (request.Description.Length > 4000)
        {
            errors["description"] = ["Опис не повинен перевищувати 4000 символів."];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        incident.Title = request.Title!.Trim();
        incident.Description = request.Description!.Trim();
        incident.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

        private static async Task<IResult> DeleteAsync(
        Guid id,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        SecureLabDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var currentUser = await userManager.GetUserAsync(principal);

        if (currentUser is null)
        {
            return Results.Unauthorized();
        }

        var isReporter = await userManager.IsInRoleAsync(
            currentUser,
            DbSeeder.ReporterRole);

        if (!isReporter)
        {
            return Results.Forbid();
        }

        var incident = await dbContext.Incidents.SingleOrDefaultAsync(
            item => item.Id == id && item.OwnerUserId == currentUser.Id,
            cancellationToken);

        if (incident is null)
        {
            return Results.Problem(
                title: "Інцидент не знайдено",
                detail: "Інцидент не існує або у вас немає прав на його видалення.",
                statusCode: StatusCodes.Status404NotFound);
        }

        dbContext.Incidents.Remove(incident);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

        private static async Task<IResult> TriageAsync(
        Guid id,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        SecureLabDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var currentUser = await userManager.GetUserAsync(principal);

        if (currentUser is null)
        {
            return Results.Unauthorized();
        }

        var isAnalyst = await userManager.IsInRoleAsync(
            currentUser,
            DbSeeder.AnalystRole);

        if (!isAnalyst)
        {
            return Results.Forbid();
        }

        var incident = await dbContext.Incidents.SingleOrDefaultAsync(
            item => item.Id == id,
            cancellationToken);

        if (incident is null)
        {
            return Results.Problem(
                title: "Інцидент не знайдено",
                statusCode: StatusCodes.Status404NotFound);
        }

        if (incident.Status != IncidentStatus.New)
        {
            return Results.Problem(
                title: "Неможливо змінити статус",
                detail: "Перевести в Triaged можна лише інцидент зі статусом New.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var now = DateTimeOffset.UtcNow;

        dbContext.IncidentStatusHistory.Add(new IncidentStatusHistory
        {
            Id = Guid.NewGuid(),
            IncidentId = incident.Id,
            ChangedByUserId = currentUser.Id,
            OldStatus = incident.Status,
            NewStatus = IncidentStatus.Triaged,
            Note = "Інцидент прийнято аналітиком на опрацювання.",
            CreatedAtUtc = now
        });

        incident.Status = IncidentStatus.Triaged;
        incident.UpdatedAtUtc = now;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> GetSeveritySummaryAsync(
        string? status,
        IncidentQueries queries,
        CancellationToken cancellationToken)
    {
        IncidentStatus? parsedStatus = null;

        if (status is not null)
        {
            if (!Enum.TryParse<IncidentStatus>(status, ignoreCase: true, out var candidate)
                || !Enum.IsDefined(candidate)
                || !string.Equals(
                    Enum.GetName(candidate),
                    status,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["status"] = ["Допустимі значення: New, Triaged, InProgress, Resolved, Closed."]
                });
            }

            parsedStatus = candidate;
        }

        var summary = await queries.GetSeveritySummaryAsync(
            parsedStatus,
            cancellationToken);

        return Results.Ok(summary);
    }
}
