﻿using jihadkhawaja.chat.server.Database;
using jihadkhawaja.chat.server.Models;
using jihadkhawaja.chat.server.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace jihadkhawaja.chat.server.Repository
{
    public abstract class BaseRepository
    {
        protected readonly DataContext _dbContext;
        protected readonly IConfiguration _configuration;
        protected readonly EncryptionService _encryptionService;
        protected readonly IHttpContextAccessor _httpContextAccessor;

        protected BaseRepository(DataContext dbContext,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration,
            EncryptionService encryptionService)
        {
            _dbContext = dbContext;
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
            _encryptionService = encryptionService;
        }

        protected virtual async Task<User?> GetConnectedUser()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                return null;
            }

            var identity = httpContext.User.Identity as ClaimsIdentity;
            var claim = identity?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            if (claim == null || !Guid.TryParse(claim.Value, out Guid userId))
            {
                return null;
            }

            return await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId);
        }

        protected virtual async Task<User?> GetConnectedUserWithDetails()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                return null;
            }

            var identity = httpContext.User.Identity as ClaimsIdentity;
            var claim = identity?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            if (claim == null || !Guid.TryParse(claim.Value, out Guid userId))
            {
                return null;
            }

            return await _dbContext.Users.Include(x => x.UserDetail).FirstOrDefaultAsync(x => x.Id == userId);
        }

        protected virtual async Task<User?> GetConnectedUserWithStorage()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                return null;
            }

            var identity = httpContext.User.Identity as ClaimsIdentity;
            var claim = identity?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            if (claim == null || !Guid.TryParse(claim.Value, out Guid userId))
            {
                return null;
            }

            return await _dbContext.Users.Include(x => x.UserStorage).FirstOrDefaultAsync(x => x.Id == userId);
        }

        protected virtual Guid? GetConnectorUserId()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext.User?.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out Guid userId))
                {
                    return userId;
                }
            }
            else
            {
                string token = httpContext?.Request.Query["access_token"] ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(token))
                {
                    var tokenHandler = new JwtSecurityTokenHandler();
                    var jwtToken = tokenHandler.ReadJwtToken(token);
                    var claim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
                    if (claim != null && Guid.TryParse(claim.Value, out Guid userId))
                    {
                        return userId;
                    }
                }
            }
            return null;
        }
    }
}
