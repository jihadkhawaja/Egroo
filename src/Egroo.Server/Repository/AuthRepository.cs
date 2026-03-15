using Egroo.Server.Authorization;
using Egroo.Server.Database;
using Egroo.Server.Helpers;
using Egroo.Server.Models;
using jihadkhawaja.chat.shared.Helpers;
using jihadkhawaja.chat.shared.Interfaces;
using jihadkhawaja.chat.shared.Models;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Egroo.Server.Repository
{
    public class AuthRepository : BaseRepository, IAuth
    {
        public AuthRepository(DataContext dbContext,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration,
            IConnectionTracker connectionTracker,
            ILogger<AuthRepository> logger)
            : base(dbContext, httpContextAccessor, configuration, connectionTracker, logger)
        {
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

        public async Task<Operation.Response> SignIn(string username, string password)
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

            UpdateUserStatus(ref user);

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
                _dbContext.Users.Update(user);
                await _dbContext.SaveChangesAsync();

                return new Operation.Response
                {
                    Success = true,
                    Message = "Sign in successful.",
                    UserId = user.Id,
                    Token = token
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

            if (!TryReadUserIdFromToken(bearerToken, out var userId, out var tokenError))
            {
                return CreateFailureResponse(tokenError);
            }

            var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId);
            if (user == null)
            {
                return CreateFailureResponse("User not found.");
            }

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
                _dbContext.Users.Update(user);
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

        private bool TryReadUserIdFromToken(string bearerToken, out Guid userId, out string errorMessage)
        {
            userId = Guid.Empty;
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

            var userIdClaim = jwtToken.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out userId) && userId != Guid.Empty;
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
