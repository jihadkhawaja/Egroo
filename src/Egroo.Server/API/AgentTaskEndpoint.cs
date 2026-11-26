using System.Security.Claims;
using Egroo.Contracts.Models;
using Egroo.Server.Models;
using Egroo.Server.Repository;

namespace Egroo.Server.API;

internal static class AgentTaskEndpoint
{
    private const string Route = "/api/v1/AgentTasks";

    internal static void MapAgentTasks(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(Route)
            .WithTags("Agent Tasks")
            .RequireRateLimiting("Api")
            .RequireAuthorization();

        group.MapPost("/", async (AgentTaskRepository repository, ClaimsPrincipal user,
            CreateAgentTaskRequest request, CancellationToken cancellationToken) =>
        {
            if (!TryGetOwner(user, out var ownerId))
                return Results.Unauthorized();

            var prompt = request.Prompt?.Trim();
            if (request.AgentId == Guid.Empty || request.RequestId == Guid.Empty
                || string.IsNullOrEmpty(prompt) || prompt.Length > AgentTaskRecord.MaxPromptLength)
            {
                return Results.BadRequest(new
                {
                    message = $"Provide an agent ID, a request ID, and a prompt of 1 to {AgentTaskRecord.MaxPromptLength} characters."
                });
            }

            var result = await repository.CreateAsync(ownerId, request.AgentId, request.RequestId, prompt, cancellationToken);
            return result.Status switch
            {
                AgentTaskSubmission.Created => Results.Created($"{Route}/{result.Task!.Id:D}", result.Task),
                AgentTaskSubmission.Existing => Results.Ok(result.Task),
                AgentTaskSubmission.Conflict => Results.Conflict(new { message = "This request ID already belongs to a different task." }),
                _ => Results.NotFound()
            };
        });

        group.MapGet("/", async (AgentTaskRepository repository, ClaimsPrincipal user,
            int? skip, int? take, CancellationToken cancellationToken) =>
        {
            if (!TryGetOwner(user, out var ownerId))
                return Results.Unauthorized();

            return Results.Ok(await repository.ListAsync(ownerId, Math.Max(skip ?? 0, 0),
                Math.Clamp(take ?? 20, 1, 50), cancellationToken));
        });

        group.MapGet("/{taskId:guid}", async (AgentTaskRepository repository, ClaimsPrincipal user,
            Guid taskId, CancellationToken cancellationToken) =>
        {
            if (!TryGetOwner(user, out var ownerId))
                return Results.Unauthorized();

            var task = await repository.GetAsync(ownerId, taskId, cancellationToken);
            return task is null ? Results.NotFound() : Results.Ok(task);
        });

        group.MapPost("/{taskId:guid}/cancel", async (AgentTaskRepository repository, ClaimsPrincipal user,
            Guid taskId, CancellationToken cancellationToken) =>
        {
            if (!TryGetOwner(user, out var ownerId))
                return Results.Unauthorized();

            var task = await repository.CancelAsync(ownerId, taskId, cancellationToken);
            return task switch
            {
                null => Results.NotFound(),
                { Status: AgentTaskStatus.Cancelled } => Results.Ok(task),
                _ => Results.Conflict(task)
            };
        });
    }

    private static bool TryGetOwner(ClaimsPrincipal user, out Guid ownerId) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out ownerId) && ownerId != Guid.Empty;
}
