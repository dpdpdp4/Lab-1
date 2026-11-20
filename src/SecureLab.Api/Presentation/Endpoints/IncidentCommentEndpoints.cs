using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;
using SecureLab.Api.Presentation.Contracts;

namespace SecureLab.Api.Presentation.Endpoints;

public static class IncidentCommentEndpoints
{
    public static IEndpointRouteBuilder MapIncidentCommentEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/incident-comments/{id:guid}", GetCommentAsync)
            .WithName("GetIncidentComment")
            .Produces<IncidentCommentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> GetCommentAsync(
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

        var isReporter = await userManager.IsInRoleAsync(
            currentUser,
            DbSeeder.ReporterRole);

        if (!isAnalyst && !isReporter)
        {
            return Results.Forbid();
        }

        var comment = await dbContext.IncidentComments
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new
            {
                item.Id,
                item.Text,
                item.CreatedAtUtc,
                item.IsInternal,
                item.IncidentId,
                item.Incident.OwnerUserId,
                AuthorName = item.Author.DisplayName
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (comment is null)
        {
            return Results.NotFound();
        }

        if (isReporter &&
            (comment.OwnerUserId != currentUser.Id || comment.IsInternal))
        {
            return Results.NotFound();
        }

        return Results.Ok(new IncidentCommentResponse(
            comment.Id,
            comment.AuthorName,
            comment.Text,
            comment.CreatedAtUtc));
    }
}