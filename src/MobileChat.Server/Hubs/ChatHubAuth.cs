using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using MobileChat.Server.Helpers;
using MobileChat.Shared.Interfaces;
using MobileChat.Shared.Models;

namespace MobileChat.Server.Hubs
{
    public partial class ChatHub : IChatAuth
    {
        public async Task<dynamic> SignUp(string displayname, string username, string email, string password)
        {
            username = username.ToLower();

            if (!string.IsNullOrWhiteSpace(email))
            {
                email = email.ToLower();
            }

            if ((await UserService.Read(x => x.Username == username)).FirstOrDefault() != null)
            {
                return null;
            }

            User user = new()
            {
                Id = Guid.NewGuid(),
                Username = username,
                Email = email,
                Password = password,
                DisplayName = displayname,
                ConnectionId = Context.ConnectionId,
                DateCreated = DateTime.UtcNow,
                IsOnline = true,
                Permission = 0
            };

            var generatedToken = await Authorization.TokenGenerator.GenerateJwtToken(user, Program.Configurations.GetSection("Secrets")["Jwt"]);
            user.Token = generatedToken.Access_Token;

            User[] users = new User[1] { user };
            if (await UserService.Create(users))
            {
                var result = new
                {
                    user.Id,
                    user.Token,
                };

                return result;
            }

            return null;
        }
        public async Task<dynamic> SignIn(string emailorusername, string password)
        {
            emailorusername = emailorusername.ToLower();

            if (PatternMatchHelper.IsEmail(emailorusername))
            {
                if ((await UserService.Read(x => x.Email == emailorusername)).FirstOrDefault() == null)
                {
                    return null;
                }

                if ((await UserService.Read(x => x.Email == emailorusername && x.Password == password)).FirstOrDefault() == null)
                {
                    return null;
                }

                User registeredUser = (await UserService.Read(x => x.Email == emailorusername)).FirstOrDefault();
                registeredUser.ConnectionId = Context.ConnectionId;
                registeredUser.IsOnline = true;

                var generatedToken = await Authorization.TokenGenerator.GenerateJwtToken(registeredUser, Program.Configurations.GetSection("Secrets")["Jwt"]);
                registeredUser.Token = generatedToken.Access_Token;

                User[] users = new User[1] { registeredUser };
                await UserService.Update(users);

                var result = new
                {
                    registeredUser.Id,
                    registeredUser.Token,
                };

                return result;
            }
            else
            {
                if ((await UserService.Read(x => x.Username == emailorusername)).FirstOrDefault() == null)
                {
                    return null;
                }

                if ((await UserService.Read(x => x.Username == emailorusername && x.Password == password)).FirstOrDefault() == null)
                {
                    return null;
                }

                User registeredUser = (await UserService.Read(x => x.Username == emailorusername)).FirstOrDefault();
                registeredUser.ConnectionId = Context.ConnectionId;
                registeredUser.IsOnline = true;

                var generatedToken = await Authorization.TokenGenerator.GenerateJwtToken(registeredUser, Program.Configurations.GetSection("Secrets")["Jwt"]);
                registeredUser.Token = generatedToken.Access_Token;

                User[] users = new User[1] { registeredUser };
                await UserService.Update(users);

                var result = new
                {
                    registeredUser.Id,
                    registeredUser.Token,
                };

                return result;
            }
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<bool> ChangePassword(string emailorusername, string oldpassword, string newpassword)
        {
            if (PatternMatchHelper.IsEmail(emailorusername))
            {
                User registeredUser = (await UserService.Read(x => x.Email == emailorusername && x.Password == oldpassword)).FirstOrDefault();

                if (registeredUser is null)
                {
                    return false;
                }

                registeredUser.Password = newpassword;

                User[] users = new User[1] { registeredUser };
                await UserService.Update(users);

                return true;
            }
            else
            {
                User registeredUser = (await UserService.Read(x => x.Username == emailorusername && x.Password == oldpassword)).FirstOrDefault();

                if (registeredUser is null)
                {
                    return false;
                }

                registeredUser.Password = newpassword;

                User[] users = new User[1] { registeredUser };
                await UserService.Update(users);

                return true;
            }
        }
    }
}