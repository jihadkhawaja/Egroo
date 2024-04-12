using jihadkhawaja.chat.server.Interfaces;
using jihadkhawaja.chat.shared.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace jihadkhawaja.chat.server.API
{
    public static class AuthAPI
    {
        public static void MapAuthentication(this IEndpointRouteBuilder routes)
        {
            var group = routes.MapGroup("/api/v1/Auth")
                .WithTags("Auth")
                .AllowAnonymous()
                .RequireRateLimiting("Api");

            //group.MapPost("/signin", async Task<Results<Ok<dynamic?>, NotFound, UnauthorizedHttpResult>>
            //    (HttpContext context,
            //    IEntity<User> UserService,
            //    string username,
            //    string password) =>
            //{

            //})
            //.WithName("SignIn")
            //.WithOpenApi();
        }
    }
}
