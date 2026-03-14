using Egroo.Server.Services;
using jihadkhawaja.chat.shared.Interfaces;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Egroo.Server.API
{
    public static class ChannelFileEndpoint
    {
        public static void MapChannelFiles(this IEndpointRouteBuilder routes)
        {
            var group = routes.MapGroup("/api/v1/ChannelFiles")
                .WithTags("Channel Files")
                .RequireRateLimiting("Api");

            group.MapPost("/{channelId:guid}", async (
                Guid channelId,
                HttpRequest request,
                IChannel channelRepository,
                ChannelFileStorageService storage,
                ClaimsPrincipal user,
                CancellationToken cancellationToken) =>
            {
                string? userIdValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!Guid.TryParse(userIdValue, out var userId) || userId == Guid.Empty)
                {
                    return Results.Unauthorized();
                }

                if (!await channelRepository.ChannelContainUser(channelId, userId))
                {
                    return Results.Forbid();
                }

                var form = await request.ReadFormAsync(cancellationToken);
                IFormFile? file = form.Files["file"] ?? form.Files.FirstOrDefault();
                if (file is null)
                {
                    return Results.BadRequest(new { error = "No file was provided." });
                }

                if (file.Length > ChannelFileStorageService.MaxFileSizeBytes)
                {
                    return Results.BadRequest(new { error = "File exceeds the 1 MB limit." });
                }

                var uploaded = await storage.SaveAsync(channelId, file, cancellationToken);
                return uploaded is null
                    ? Results.BadRequest(new { error = "Failed to store file." })
                    : Results.Ok(uploaded);
            })
            .DisableAntiforgery()
            .RequireAuthorization();

            group.MapGet("/{channelId:guid}/{token}/{fileName}", async (
                Guid channelId,
                string token,
                string fileName,
                IChannel channelRepository,
                ChannelFileStorageService storage,
                ClaimsPrincipal user) =>
            {
                string? userIdValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!Guid.TryParse(userIdValue, out var userId) || userId == Guid.Empty)
                {
                    return Results.Unauthorized();
                }

                if (!await channelRepository.ChannelContainUser(channelId, userId))
                {
                    return Results.Forbid();
                }

                if (!storage.TryResolveFile(channelId, token, fileName, out var filePath, out var contentType, out var downloadFileName))
                {
                    return Results.NotFound();
                }

                return Results.File(filePath, contentType, downloadFileName, enableRangeProcessing: true);
            })
            .RequireAuthorization();
        }
    }
}