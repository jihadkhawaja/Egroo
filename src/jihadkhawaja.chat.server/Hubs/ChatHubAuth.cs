using jihadkhawaja.chat.server.Authorization;
using jihadkhawaja.chat.server.Helpers;
using jihadkhawaja.chat.shared.Helpers;
using jihadkhawaja.chat.shared.Interfaces;
using jihadkhawaja.chat.shared.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace jihadkhawaja.chat.server.Hubs
{
    public partial class ChatHub : IChatAuth
    {
        private void UpdateUserOnlineStatus(User user)
        {
            user.ConnectionId = Context.ConnectionId;
            user.IsOnline = true;
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

            if (await UserService.ReadFirst(x => x.Username == username) != null)
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
                ConnectionId = Context.ConnectionId,
                IsOnline = true,
                LastLoginDate = DateTimeOffset.UtcNow,
                DateUpdated = DateTimeOffset.UtcNow
            };

            if (Configuration == null)
            {
                return new Operation.Response
                {
                    Success = false,
                    Message = "Server configuration not available."
                };
            }

            var jwtSecret = Configuration.GetSection("Secrets")["Jwt"];
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

            if (await UserService.Create(user))
            {
                return new Operation.Response
                {
                    Success = true,
                    Message = "User created successfully.",
                    UserId = user.Id,
                    Token = token
                };
            }

            return new Operation.Response
            {
                Success = false,
                Message = "Failed to create user."
            };
        }

        public async Task<Operation.Response> SignIn(string username, string password)
        {
            username = username.ToLower();

            var user = await UserService.ReadFirst(x => x.Username == username);
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

            if (Configuration == null)
            {
                return new Operation.Response
                {
                    Success = false,
                    Message = "Server configuration not available."
                };
            }

            UpdateUserOnlineStatus(user);

            var jwtSecret = Configuration.GetSection("Secrets")["Jwt"];
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

            await UserService.Update(user);

            return new Operation.Response
            {
                Success = true,
                Message = "Sign in successful.",
                UserId = user.Id,
                Token = token
            };
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

            var user = await UserService.ReadFirst(x => x.Id == userId);
            if (user == null)
            {
                return new Operation.Response
                {
                    Success = false,
                    Message = "User not found."
                };
            }

            if (Configuration == null)
            {
                return new Operation.Response
                {
                    Success = false,
                    Message = "Server configuration not available."
                };
            }

            var jwtSecret = Configuration.GetSection("Secrets")["Jwt"];
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

            UpdateUserOnlineStatus(user);
            await UserService.Update(user);

            return new Operation.Response
            {
                Success = true,
                Message = "Session refreshed.",
                UserId = user.Id,
                Token = newToken
            };
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<Operation.Result> ChangePassword(string username, string oldpassword, string newpassword)
        {
            var registeredUser = await UserService.ReadFirst(x => x.Username == username);
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

            if (await UserService.Update(registeredUser))
            {
                return new Operation.Result
                {
                    Success = true,
                    Message = "Password changed successfully."
                };
            }

            return new Operation.Result
            {
                Success = false,
                Message = "Failed to update password."
            };
        }
    }
}
