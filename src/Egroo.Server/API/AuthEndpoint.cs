using Egroo.Contracts.Interfaces;
using System.Diagnostics.CodeAnalysis;

namespace Egroo.Server.API
{
    public static class AuthEndpoint
    {
        public static void MapAuthentication(this IEndpointRouteBuilder routes)
        {
            var group = routes.MapGroup("/api/v1/Auth")
                .WithTags("Auth")
                .RequireRateLimiting("Api");

            group.AddEndpointFilter((invocation, next) =>
            {
                invocation.HttpContext.Response.Headers.CacheControl = "no-store";
                return next(invocation);
            });

            group.MapPost("/signup", async (IAuth auth, SignUpRequest req) =>
            {
                return await auth.SignUp(req.Username, req.Password);
            }).AllowAnonymous();

            group.MapPost("/signin", async (ITwoFactorAuth auth, SignInRequest req) =>
            {
                return await auth.SignIn(req.Username, req.Password, req.VerificationCode);
            }).AllowAnonymous();

            group.MapGet("/refreshsession", async (IAuth auth) =>
            {
                return await auth.RefreshSession();
            }).RequireAuthorization();

            group.MapPut("/changepassword", async (IAuth auth, ChangePasswordRequest req) =>
            {
                return await auth.ChangePassword(req.OldPassword, req.NewPassword);
            }).RequireAuthorization();

            var twoFactor = group.MapGroup("/twofactor").RequireAuthorization();
            twoFactor.MapGet("/", (ITwoFactorAuth auth) => auth.GetTwoFactorStatus());
            twoFactor.MapPost("/enrollment", (ITwoFactorAuth auth, TwoFactorEnrollmentRequest req) =>
                auth.BeginTwoFactorEnrollment(req.Password));
            twoFactor.MapPost("/enable", (ITwoFactorAuth auth, TwoFactorVerificationRequest req) =>
                auth.EnableTwoFactor(req.Password, req.VerificationCode));
            twoFactor.MapPost("/disable", (ITwoFactorAuth auth, TwoFactorVerificationRequest req) =>
                auth.DisableTwoFactor(req.Password, req.VerificationCode));
        }
    }

    [ExcludeFromCodeCoverage]
    public record SignUpRequest(string Username, string Password);

    [ExcludeFromCodeCoverage]
    public record SignInRequest(string Username, string Password, string? VerificationCode = null);

    [ExcludeFromCodeCoverage]
    public record ChangePasswordRequest(string OldPassword, string NewPassword);

    [ExcludeFromCodeCoverage]
    public record TwoFactorEnrollmentRequest(string Password);

    [ExcludeFromCodeCoverage]
    public record TwoFactorVerificationRequest(string Password, string VerificationCode);
}
