using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using MobileChat.Server.Helpers;
using MobileChat.Shared.Interfaces;
using MobileChat.Shared.Models;

namespace MobileChat.Server.Hubs
{
    public partial class ChatHub : IChatUser
    {
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<string> GetUserDisplayName(Guid userId)
        {
            string displayname = (await UserService.Read(x => x.Id == userId)).FirstOrDefault().DisplayName;

            return displayname;
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<string> GetUserUsername(Guid userId)
        {
            string username = (await UserService.Read(x => x.Id == userId)).FirstOrDefault().Username;

            return username;
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<bool> AddFriend(string friendEmailorusername)
        {
            if (string.IsNullOrEmpty(friendEmailorusername))
            {
                return false;
            }

            try
            {
                string Token = Context.GetHttpContext().Request.Query["access_token"];
                Guid ConnectorUserId = (await UserService.Read(x => x.Token == Token)).FirstOrDefault().Id;

                if (PatternMatchHelper.IsEmail(friendEmailorusername))
                {
                    //get user id from email
                    User user = (await UserService.Read(x => x.Id == ConnectorUserId)).FirstOrDefault();
                    if (user == null)
                    {
                        return false;
                    }
                    //get friend id from email
                    User friendUser = (await UserService.Read(x => x.Email == friendEmailorusername)).FirstOrDefault();
                    if (friendUser == null)
                    {
                        return false;
                    }

                    if ((await UserFriendsService.Read(x => x.UserId == user.Id && x.FriendUserId == friendUser.Id
                    || x.FriendUserId == user.Id && x.UserId == friendUser.Id)).FirstOrDefault() != null)
                    {
                        return false;
                    }

                    UserFriend entry = new() { UserId = user.Id, FriendUserId = friendUser.Id, DateCreated = DateTime.UtcNow };
                    UserFriend[] entries = new UserFriend[1] { entry };
                    await UserFriendsService.Create(entries);
                }
                else
                {
                    //get user id from username
                    User user = (await UserService.Read(x => x.Id == ConnectorUserId)).FirstOrDefault();
                    if (user == null)
                    {
                        return false;
                    }
                    //get friend id from username
                    User friendUser = (await UserService.Read(x => x.Username == friendEmailorusername)).FirstOrDefault();
                    if (friendUser == null)
                    {
                        return false;
                    }

                    if ((await UserFriendsService.Read(x => x.UserId == user.Id && x.FriendUserId == friendUser.Id
                    || x.FriendUserId == user.Id && x.UserId == friendUser.Id)).FirstOrDefault() != null)
                    {
                        return false;
                    }

                    UserFriend entry = new() { Id = Guid.NewGuid(), UserId = user.Id, FriendUserId = friendUser.Id, DateCreated = DateTime.UtcNow };
                    UserFriend[] entries = new UserFriend[1] { entry };
                    await UserFriendsService.Create(entries);
                }
            }
            catch
            {
                return false;
            }

            return true;
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<bool> RemoveFriend(string friendEmailorusername)
        {
            if (string.IsNullOrEmpty(friendEmailorusername))
            {
                return false;
            }

            try
            {
                string Token = Context.GetHttpContext().Request.Query["access_token"];
                Guid ConnectorUserId = (await UserService.Read(x => x.Token == Token)).FirstOrDefault().Id;

                if (PatternMatchHelper.IsEmail(friendEmailorusername))
                {
                    //get user id from email
                    User user = (await UserService.Read(x => x.Id == ConnectorUserId)).FirstOrDefault();
                    if (user == null)
                    {
                        return false;
                    }
                    //get friend id from email
                    User friendUser = (await UserService.Read(x => x.Email == friendEmailorusername)).FirstOrDefault();
                    if (friendUser == null)
                    {
                        return false;
                    }

                    if ((await UserFriendsService.Read(x => x.UserId == user.Id && x.FriendUserId == friendUser.Id
                    || x.FriendUserId == user.Id && x.UserId == friendUser.Id)).FirstOrDefault() != null)
                    {
                        return false;
                    }

                    UserFriend entry = new() { UserId = user.Id, FriendUserId = friendUser.Id, DateCreated = DateTime.UtcNow };

                    await UserFriendsService.Delete(x => x.Id == entry.Id);
                }
                else
                {
                    //get user id from username
                    User user = (await UserService.Read(x => x.Id == ConnectorUserId)).FirstOrDefault();
                    if (user == null)
                    {
                        return false;
                    }
                    //get friend id from username
                    User friendUser = (await UserService.Read(x => x.Username == friendEmailorusername)).FirstOrDefault();
                    if (friendUser == null)
                    {
                        return false;
                    }

                    if ((await UserFriendsService.Read(x => x.UserId == user.Id && x.FriendUserId == friendUser.Id
                    || x.FriendUserId == user.Id && x.UserId == friendUser.Id)).FirstOrDefault() == null)
                    {
                        return false;
                    }

                    UserFriend entry = new() { UserId = user.Id, FriendUserId = friendUser.Id, DateCreated = DateTime.UtcNow };

                    await UserFriendsService.Delete(x => x.UserId == user.Id && x.FriendUserId == friendUser.Id
                    || x.FriendUserId == user.Id && x.UserId == friendUser.Id);
                }
            }
            catch
            {
                return false;
            }

            return true;
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<UserFriend[]> GetUserFriends(Guid userId)
        {
            return (await UserFriendsService.Read(x => x.UserId == userId && x.IsAccepted || x.FriendUserId == userId && x.IsAccepted)).ToArray();
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<UserFriend[]> GetUserFriendRequests(Guid userId)
        {
            return (await UserFriendsService.Read(x => x.FriendUserId == userId && !x.IsAccepted)).ToArray();
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<bool> GetUserIsFriend(Guid userId, Guid friendId)
        {
            UserFriend result = (await UserFriendsService.Read(x => x.UserId == userId && x.FriendUserId == friendId && x.IsAccepted)).FirstOrDefault();

            if (result is null)
            {
                return false;
            }

            return result.IsAccepted;
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<bool> AcceptFriend(Guid friendId)
        {
            string Token = Context.GetHttpContext().Request.Query["access_token"];
            Guid ConnectorUserId = (await UserService.Read(x => x.Token == Token)).FirstOrDefault().Id;

            UserFriend friendRequest = (await UserFriendsService.Read(x => x.UserId == friendId && x.FriendUserId == ConnectorUserId && !x.IsAccepted)).FirstOrDefault();

            if (friendRequest is null)
            {
                return false;
            }

            friendRequest.IsAccepted = true;

            UserFriend[] friendRequests = new UserFriend[1] { friendRequest };
            return await UserFriendsService.Update(friendRequests);
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<bool> DenyFriend(Guid friendId)
        {
            string Token = Context.GetHttpContext().Request.Query["access_token"];
            Guid ConnectorUserId = (await UserService.Read(x => x.Token == Token)).FirstOrDefault().Id;

            return await UserFriendsService.Delete(x => x.UserId == friendId && x.FriendUserId == ConnectorUserId && !x.IsAccepted);
        }
    }
}