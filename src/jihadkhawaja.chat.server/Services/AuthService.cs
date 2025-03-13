using jihadkhawaja.chat.server.Authorization;
using jihadkhawaja.chat.server.Database;
using jihadkhawaja.chat.server.Helpers;
using jihadkhawaja.chat.shared.Helpers;
using jihadkhawaja.chat.shared.Interfaces;
using jihadkhawaja.chat.shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace jihadkhawaja.chat.server.Services
{
    public class AuthService : IAuth
    {
        private readonly IConfiguration _configuration;
        private readonly DataContext _dbContext;

        public AuthService(DataContext dbContext, IConfiguration configuration)
        {
            _dbContext = dbContext;
            _configuration = configuration;
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
            catch (Exception)
            {
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
            catch (Exception)
            {
                return new Operation.Response
                {
                    Success = false,
                    Message = "Failed to update user."
                };
            }
        }

        public async Task<Operation.Response> RefreshSession(string oldtoken)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            JwtSecurityToken jwtToken;

            try
            {
                jwtToken = tokenHandler.ReadJwtToken(oldtoken);
            }
            catch (Exception)
            {
                return new Operation.Response
                {
                    Success = false,
                    Message = "Invalid token format."
                };
            }

            var userIdClaim = jwtToken.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out Guid userId) || userId == Guid.Empty)
            {
                return new Operation.Response
                {
                    Success = false,
                    Message = "Invalid token claims."
                };
            }

            var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId);
            if (user == null)
            {
                return new Operation.Response
                {
                    Success = false,
                    Message = "User not found."
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
            catch (Exception)
            {
                return new Operation.Response
                {
                    Success = false,
                    Message = "Failed to update user."
                };
            }
        }

        public async Task<Operation.Result> ChangePassword(string username, string oldpassword, string newpassword)
        {
            var registeredUser = await _dbContext.Users.FirstOrDefaultAsync(x => x.Username == username);
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
            catch (Exception)
            {
                return new Operation.Result
                {
                    Success = false,
                    Message = "Failed to update password."
                };
            }
        }
    }
}
