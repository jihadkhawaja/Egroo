using MobileChat.Server.Helpers;
using MobileChat.Shared.Interfaces;
using MobileChat.Shared.Models;

namespace MobileChat.Server.Hubs
{
    public partial class ChatHub : IChatUser
    {
        public async Task<string> GetUserDisplayName(Guid userId)
        {
            string displayname = (await UserService.Read(x => x.Id == userId)).FirstOrDefault().DisplayName;

            return displayname;
        }

        public async Task<string> GetUserUsername(Guid userId)
        {
            string username = (await UserService.Read(x => x.Id == userId)).FirstOrDefault().Username;

            return username;
        }

        public async Task<bool> AddFriend(Guid userId, string friendEmailorusername)
        {
            if (string.IsNullOrEmpty(friendEmailorusername))
            {
                return false;
            }

            try
            {
                if (PatternMatchHelper.IsEmail(friendEmailorusername))
                {
                    //get user id from email
                    User user = (await UserService.Read(x => x.Id == userId)).FirstOrDefault();
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
                    User user = (await UserService.Read(x => x.Id == userId)).FirstOrDefault();
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
        public async Task<bool> RemoveFriend(Guid userId, string friendEmailorusername)
        {
            if (string.IsNullOrEmpty(friendEmailorusername))
            {
                return false;
            }

            try
            {
                if (PatternMatchHelper.IsEmail(friendEmailorusername))
                {
                    //get user id from email
                    User user = (await UserService.Read(x => x.Id == userId)).FirstOrDefault();
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
                    User user = (await UserService.Read(x => x.Id == userId)).FirstOrDefault();
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

        public async Task<UserFriend[]> GetUserFriends(Guid userId)
        {
            return (await UserFriendsService.Read(x => x.UserId == userId && x.IsAccepted || x.FriendUserId == userId && x.IsAccepted)).ToArray();
        }

        public async Task<UserFriend[]> GetUserFriendRequests(Guid userId)
        {
            return (await UserFriendsService.Read(x => x.FriendUserId == userId && !x.IsAccepted)).ToArray();
        }

        public async Task<bool> GetUserIsFriend(Guid userId, Guid friendId)
        {
            UserFriend result = (await UserFriendsService.Read(x => x.UserId == userId && x.FriendUserId == friendId && x.IsAccepted)).FirstOrDefault();

            if (result is null)
            {
                return false;
            }

            return result.IsAccepted;
        }

        public async Task<bool> AcceptFriend(Guid userId, Guid friendId)
        {
            UserFriend friendRequest = (await UserFriendsService.Read(x => x.UserId == friendId && x.FriendUserId == userId && !x.IsAccepted)).FirstOrDefault();

            if (friendRequest is null)
            {
                return false;
            }

            friendRequest.IsAccepted = true;

            UserFriend[] friendRequests = new UserFriend[1] { friendRequest };
            return await UserFriendsService.Update(friendRequests);
        }

        public async Task<bool> DenyFriend(Guid userId, Guid friendId)
        {
            return await UserFriendsService.Delete(x => x.UserId == friendId && x.FriendUserId == userId && !x.IsAccepted);
        }
    }
}