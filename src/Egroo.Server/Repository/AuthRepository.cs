using Egroo.Server.Services;
using Egroo.Server.Authorization;
using Egroo.Server.Database;
using Egroo.Server.Helpers;
using Egroo.Server.Models;
using Egroo.Server.Security;
using Egroo.Contracts.Helpers;
using Egroo.Contracts.Interfaces;
using Egroo.Contracts.Models;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Egroo.Server.Repository
{
    public partial class AuthRepository : BaseRepository, IAuth, ITwoFactorAuth, IAccountRecovery, IIpRegionAuth
    {
        private readonly TimeProvider _timeProvider;
        private readonly IRecoveryEmailDelivery? _recoveryEmail;
        private readonly AccountSessions? _accountSessions;
        private readonly IpRegionPolicy _ipRegionPolicy;

        public AuthRepository(DataContext dbContext,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration,
            IConnectionTracker connectionTracker,
            ILogger<AuthRepository> logger,
            TimeProvider? timeProvider = null,
            IRecoveryEmailDelivery? recoveryEmail = null,
            AccountSessions? accountSessions = null,
            IpRegionPolicy? ipRegionPolicy = null)
            : base(dbContext, httpContextAccessor, configuration, connectionTracker, logger)
        {
            _timeProvider = timeProvider ?? TimeProvider.System;
            _recoveryEmail = recoveryEmail;
            _accountSessions = accountSessions;
            _ipRegionPolicy = ipRegionPolicy ?? IpRegionPolicy.Disabled;
        }

        private void UpdateUserStatus(ref User user)
        {
            user.LastLoginDate = DateTimeOffset.UtcNow;
            user.DateUpdated = DateTimeOffset.UtcNow;
        }

        public async Task<Operation.Response> SignUp(string username, string password)
        {
            if (!PatternMatchHelper.IsValidUsername(username) ||
                !PatternMatchHelper.IsValidPassword(password))
            {
                return new Operation.Response
                {
                    Success = false,
                    Message = "Invalid username or password format."
                };
            }

            username = username.ToLower();

            if (await _dbContext.Users.FirstOrDefaultAsync(x => x.Username == username) != null)
            {
                return new Operation.Response
                {
                    Success = false,
                    Message = "Username already exists."
                };
            }

            string encryptedPassword = CryptographyHelper.SecurePassword(password);

            var user = new User
            {
                Username = username,
                Password = encryptedPassword,
                Role = "Member",
                LastLoginDate = DateTimeOffset.UtcNow,
                DateUpdated = DateTimeOffset.UtcNow
            };

            if (_configuration == null)
            {
                return new Operation.Response
                {
                    Success = false,
                    Message = "Server configuration not available."
                };
            }

            var jwtSecret = _configuration.GetSection("Secrets")["Jwt"];
            if (string.IsNullOrEmpty(jwtSecret))
            {
                return new Operation.Response
                {
                    Success = false,
                    Message = "JWT configuration is missing."
                };
            }

            var generatedToken = await TokenGenerator.GenerateJwtToken(user, jwtSecret);
            string token = generatedToken.Access_Token;

            try
            {
                await _dbContext.Users.AddAsync(user);
                await _dbContext.SaveChangesAsync();

                return new Operation.Response
                {
                    Success = true,
                    Message = "User created successfully.",
                    UserId = user.Id,
                    Token = token
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create user.");
                return new Operation.Response
                {
                    Success = false,
                    Message = "Failed to create user."
                };
            }
        }

        public Task<Operation.Response> SignIn(string username, string password) =>
            SignIn(username, password, verificationCode: null);

        public async Task<Operation.Response> SignIn(string username, string password, string? verificationCode)
        {
            username = username.ToLower();

            var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Username == username);
            if (user == null)
            {
                return new Operation.Response
                {
                    Success = false,
                    Message = "User does not exist."
                };
            }

            if (string.IsNullOrWhiteSpace(user.Password))
            {
                return new Operation.Response
                {
                    Success = false,
                    Message = "User password is not set."
                };
            }

            if (!CryptographyHelper.ComparePassword(password, user.Password))
            {
                return new Operation.Response
                {
                    Success = false,
                    Message = "Incorrect password."
                };
            }

            if (_configuration == null)
            {
                return new Operation.Response
                {
                    Success = false,
                    Message = "Server configuration not available."
                };
            }

            var jwtSecret = _configuration.GetSection("Secrets")["Jwt"];
            if (string.IsNullOrEmpty(jwtSecret))
            {
                return new Operation.Response
                {
                    Success = false,
                    Message = "JWT configuration is missing."
                };
            }

            if (await CheckSignInFactorAsync(user, verificationCode) is { } failure)
                return failure;

            UpdateUserStatus(ref user);
            var generatedToken = await TokenGenerator.GenerateJwtToken(user, jwtSecret);
            string token = generatedToken.Access_Token;

            try
            {
                await _dbContext.SaveChangesAsync();

                return new Operation.Response
                {
                    Success = true,
                    Message = "Sign in successful.",
                    UserId = user.Id,
                    Token = token
                };
            }
            catch (DbUpdateConcurrencyException) when (user.UserSecuriy is { IsTwoFactorEnabled: true } state)
            {
                bool requiresRecovery = RequiresRegionalRecovery(state);
                return new Operation.Response
                {
                    RequiresTwoFactor = true,
                    RequiresRecoveryCode = requiresRecovery,
                    Message = requiresRecovery ? RegionRecoveryPrompt : RejectedFactor
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update user.");
                return new Operation.Response
                {
                    Success = false,
                    Message = "Failed to update user."
                };
            }
        }

        public async Task<Operation.Response> RefreshSession()
        {
            var bearerToken = GetBearerToken();
            if (bearerToken is null)
            {
                return CreateFailureResponse("Authorization header is missing.");
            }

            if (!TryReadUserIdFromToken(bearerToken, out var userId, out var revision, out var tokenError))
            {
                return CreateFailureResponse(tokenError);
            }

            var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId);
            if (user == null)
            {
                return CreateFailureResponse("User not found.");
            }

            if (user.SessionRevision != revision)
                return CreateFailureResponse("Your session has expired. Sign in again.");

            var jwtSecret = GetJwtSecret();
            if (jwtSecret is null)
            {
                return CreateFailureResponse("JWT configuration is missing.");
            }

            var generatedToken = await TokenGenerator.GenerateJwtToken(user, jwtSecret);
            string newToken = generatedToken.Access_Token;

            UpdateUserStatus(ref user);

            try
            {
                await _dbContext.SaveChangesAsync();

                return new Operation.Response
                {
                    Success = true,
                    Message = "Session refreshed.",
                    UserId = user.Id,
                    Token = newToken
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update user.");
                return new Operation.Response
                {
                    Success = false,
                    Message = "Failed to update user."
                };
            }
        }

        private string? GetBearerToken()
        {
            var authHeader = _httpContextAccessor.HttpContext?.Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(authHeader))
            {
                return null;
            }

            var parts = authHeader.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || !parts[0].Equals("Bearer", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return parts[1];
        }

        private bool TryReadUserIdFromToken(string bearerToken, out Guid userId, out long revision, out string errorMessage)
        {
            userId = Guid.Empty;
            revision = 0;
            errorMessage = bearerToken.Length == 0
                ? "Invalid Authorization header format."
                : "Invalid token claims.";

            if (bearerToken.Length == 0)
            {
                return false;
            }

            JwtSecurityToken jwtToken;
            try
            {
                jwtToken = new JwtSecurityTokenHandler().ReadJwtToken(bearerToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read JWT token.");
                errorMessage = "Invalid token format.";
                return false;
            }

            var principal = new ClaimsPrincipal(new ClaimsIdentity(jwtToken.Claims));
            return AccountSessions.TryReadSession(principal, out userId, out revision);
        }

        private string? GetJwtSecret()
        {
            return _configuration?.GetSection("Secrets")["Jwt"];
        }

        private static Operation.Response CreateFailureResponse(string message)
        {
            return new Operation.Response
            {
                Success = false,
                Message = message
            };
        }

        public async Task<Operation.Result> ChangePassword(string oldpassword, string newpassword)
        {
            var registeredUser = await GetConnectedUser();
            if (registeredUser == null)
            {
                return new Operation.Result
                {
                    Success = false,
                    Message = "User not found."
                };
            }

            if (string.IsNullOrWhiteSpace(registeredUser.Password))
            {
                return new Operation.Result
                {
                    Success = false,
                    Message = "User password is not set."
                };
            }

            if (!CryptographyHelper.ComparePassword(oldpassword, registeredUser.Password))
            {
                return new Operation.Result
                {
                    Success = false,
                    Message = "Incorrect current password."
                };
            }

            registeredUser.Password = CryptographyHelper.SecurePassword(newpassword);
            registeredUser.LastLoginDate = DateTimeOffset.UtcNow;
            registeredUser.DateUpdated = DateTimeOffset.UtcNow;

            try
            {
                _dbContext.Users.Update(registeredUser);
                await _dbContext.SaveChangesAsync();

                return new Operation.Result
                {
                    Success = true,
                    Message = "Password changed successfully."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update password.");
                return new Operation.Result
                {
                    Success = false,
                    Message = "Failed to update password."
                };
            }
        }
    }
}
