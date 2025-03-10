﻿using jihadkhawaja.chat.shared.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace jihadkhawaja.chat.server.API
{
    public static class AuthEndpoint
    {
        public static void MapAuthentication(this IEndpointRouteBuilder routes)
        {
            var group = routes.MapGroup("/api/v1/Auth")
                .WithTags("Auth")
                .RequireRateLimiting("Api");

            group.MapPost("/signup", async (IAuth auth, SignUpRequest req) =>
            {
                return await auth.SignUp(req.Username, req.Password);
            }).AllowAnonymous();

            group.MapPost("/signin", async (IAuth auth, SignInRequest req) =>
            {
                return await auth.SignIn(req.Username, req.Password);
            }).AllowAnonymous();

            group.MapPost("/refreshsession", async (IAuth auth, RefreshSessionRequest req) =>
            {
                return await auth.RefreshSession(req.OldToken);
            });

            group.MapPost("/changepassword", async (IAuth auth, ChangePasswordRequest req) =>
            {
                return await auth.ChangePassword(req.Username, req.OldPassword, req.NewPassword);
            });
        }
    }

    public record SignUpRequest(string Username, string Password);

    public record SignInRequest(string Username, string Password);

    public record RefreshSessionRequest(string OldToken);

    public record ChangePasswordRequest(string Username, string OldPassword, string NewPassword);
}
