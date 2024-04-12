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
        public async Task<dynamic?> SignUp(string username, string password)
        {
            if (!PatternMatchHelper.IsValidUsername(username)
                || !PatternMatchHelper.IsValidPassword(password))
            {
                return null;
            }

            username = username.ToLower();

            if (await UserService.ReadFirst(x => x.Username == username) != null)
            {
                return null;
            }

            string encryptedPassword = CryptographyHelper.SecurePassword(password);

            User user = new()
            {
                Username = username,
                Password = encryptedPassword,
                ConnectionId = Context.ConnectionId,
                IsOnline = true,
                Role = "Member",
                LastLoginDate = DateTimeOffset.UtcNow,
                DateUpdated = DateTimeOffset.UtcNow
            };

            if (Configuration == null)
            {
                return null;
            }

            var generatedToken = await TokenGenerator.GenerateJwtToken(
                user, Configuration.GetSection("Secrets")["Jwt"]);
            string token = generatedToken.Access_Token;

            if (await UserService.Create(user))
            {
                var result = new
                {
                    user.Id,
                    token,
                };

                return result;
            }

            return null;
        }
        public async Task<dynamic?> SignIn(string username, string password)
        {
            username = username.ToLower();

            User? user = await UserService.ReadFirst(x => x.Username == username);
            if (user == null)
            {
                return null;
            }
            else if (string.IsNullOrWhiteSpace(user.Password))
            {
                return null;
            }
            else if (!CryptographyHelper.ComparePassword(password, user.Password))
            {
                return null;
            }
            else if (Configuration == null)
            {
                return null;
            }

            User registeredUser = user;

            registeredUser.ConnectionId = Context.ConnectionId;
            registeredUser.IsOnline = true;
            registeredUser.LastLoginDate = DateTimeOffset.UtcNow;
            registeredUser.DateUpdated = DateTimeOffset.UtcNow;

            var generatedToken = await TokenGenerator.GenerateJwtToken(
                registeredUser, Configuration.GetSection("Secrets")["Jwt"]);
            string token = generatedToken.Access_Token;

            await UserService.Update(registeredUser);

            var result = new
            {
                registeredUser.Id,
                token,
            };

            return result;
        }
        public async Task<dynamic?> RefreshSession(string oldtoken)
        {
            //get claims from old token
            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.ReadJwtToken(oldtoken);
            var claims = token.Claims;

            //get user from claims
            Guid.TryParse(claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier)?.Value, out Guid userId);
            if (userId == Guid.Empty)
            {
                return null;
            }

            User? user = await UserService.ReadFirst(x => x.Id == userId);
            if (user == null)
            {
                return null;
            }

            var generatedToken = await TokenGenerator.GenerateJwtToken(
                user, Configuration.GetSection("Secrets")["Jwt"]);
            string newtoken = generatedToken.Access_Token;

            user.ConnectionId = Context.ConnectionId;
            user.IsOnline = true;
            user.LastLoginDate = DateTimeOffset.UtcNow;
            user.DateUpdated = DateTimeOffset.UtcNow;
            await UserService.Update(user);

            var result = new
            {
                user.Id,
                Token = newtoken,
            };

            return result;
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<bool> ChangePassword(string username, string oldpassword, string newpassword)
        {
            User? registeredUser = await UserService.ReadFirst(x => x.Username == username);

            if (registeredUser is null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(registeredUser.Password))
            {
                return false;
            }
            else if (!CryptographyHelper.ComparePassword(oldpassword, registeredUser.Password))
            {
                return false;
            }

            string encryptedPassword = CryptographyHelper.SecurePassword(newpassword);
            registeredUser.Password = encryptedPassword;

            registeredUser.LastLoginDate = DateTimeOffset.UtcNow;
            registeredUser.DateUpdated = DateTimeOffset.UtcNow;
            await UserService.Update(registeredUser);

            return true;
        }
    }
}