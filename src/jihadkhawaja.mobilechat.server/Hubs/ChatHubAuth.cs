using jihadkhawaja.mobilechat.server.Authorization;
using jihadkhawaja.mobilechat.server.Helpers;
using jihadkhawaja.mobilechat.server.Interfaces;
using jihadkhawaja.mobilechat.server.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace jihadkhawaja.mobilechat.server.Hubs
{
    public partial class ChatHub : IChatAuth
    {
        public async Task<dynamic?> SignUp(string displayname, string username, string email, string password)
        {
            if (!PatternMatchHelper.IsValidUsername(username)
                || !PatternMatchHelper.IsValidPassword(password))
            {
                return null;
            }

            username = username.ToLower();

            if (!string.IsNullOrWhiteSpace(email))
            {
                if (PatternMatchHelper.IsValidEmail(email))
                {
                    email = email.ToLower();
                }
                else
                {
                    email = string.Empty;
                }
            }

            if (await UserService.ReadFirst(x => x.Username == username) != null)
            {
                return null;
            }

            string encryptedPassword = CryptographyHelper.SecurePassword(password);

            User user = new()
            {
                Username = username,
                Email = email,
                Password = encryptedPassword,
                DisplayName = displayname,
                ConnectionId = Context.ConnectionId,
                IsOnline = true,
                Role = "Member",
                LastLoginDate = DateTimeOffset.UtcNow,
                DateUpdated = DateTimeOffset.UtcNow
            };

            if (MobileChatServer.Configuration == null)
            {
                return null;
            }

            var generatedToken = await TokenGenerator.GenerateJwtToken(user, MobileChatServer.Configuration.GetSection("Secrets")["Jwt"]);
            string token = generatedToken.Access_Token;

            User[] users = new User[1] { user };
            if (await UserService.Create(users))
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
        public async Task<dynamic?> SignIn(string emailorusername, string password)
        {
            emailorusername = emailorusername.ToLower();

            if (PatternMatchHelper.IsValidEmail(emailorusername))
            {
                User? user = await UserService.ReadFirst(x => x.Email == emailorusername);

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
                else if (MobileChatServer.Configuration == null)
                {
                    return null;
                }

                User registeredUser = user;

                registeredUser.ConnectionId = Context.ConnectionId;
                registeredUser.IsOnline = true;
                registeredUser.LastLoginDate = DateTimeOffset.UtcNow;
                registeredUser.DateUpdated = DateTimeOffset.UtcNow;

                var generatedToken = await TokenGenerator.GenerateJwtToken(registeredUser, MobileChatServer.Configuration.GetSection("Secrets")["Jwt"]);
                string token = generatedToken.Access_Token;

                User[] users = new User[1] { registeredUser };
                await UserService.Update(users);

                var result = new
                {
                    registeredUser.Id,
                    token,
                };

                return result;
            }
            else
            {
                User? user = await UserService.ReadFirst(x => x.Username == emailorusername);
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
                else if (MobileChatServer.Configuration == null)
                {
                    return null;
                }

                User registeredUser = user;

                registeredUser.ConnectionId = Context.ConnectionId;
                registeredUser.IsOnline = true;
                registeredUser.LastLoginDate = DateTimeOffset.UtcNow;
                registeredUser.DateUpdated = DateTimeOffset.UtcNow;

                var generatedToken = await TokenGenerator.GenerateJwtToken(registeredUser, MobileChatServer.Configuration.GetSection("Secrets")["Jwt"]);
                string token = generatedToken.Access_Token;

                User[] users = new User[1] { registeredUser };
                await UserService.Update(users);

                var result = new
                {
                    registeredUser.Id,
                    token,
                };

                return result;
            }
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

            var generatedToken = await TokenGenerator.GenerateJwtToken(user, MobileChatServer.Configuration.GetSection("Secrets")["Jwt"]);
            string newtoken = generatedToken.Access_Token;

            user.DateUpdated = DateTimeOffset.UtcNow;
            User[] users = new User[1] { user };
            await UserService.Update(users);

            var result = new
            {
                Id = user.Id,
                Token = newtoken,
            };

            return result;
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<bool> ChangePassword(string emailorusername, string oldpassword, string newpassword)
        {
            if (PatternMatchHelper.IsValidEmail(emailorusername))
            {
                User? registeredUser = await UserService.ReadFirst(x => x.Email == emailorusername);

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
                User[] users = new User[1] { registeredUser };
                await UserService.Update(users);

                return true;
            }
            else
            {
                User? registeredUser = await UserService.ReadFirst(x => x.Username == emailorusername);

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
                User[] users = new User[1] { registeredUser };
                await UserService.Update(users);

                return true;
            }
        }
    }
}