using Egroo.Server.Models;
using jihadkhawaja.chat.shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Egroo.Server.Database
{
    public interface IDataEntities
    {
        DbSet<User> Users { get; set; }
        DbSet<UserFriend> UsersFriends { get; set; }
        DbSet<Channel> Channels { get; set; }
        DbSet<ChannelUser> ChannelUsers { get; set; }
        DbSet<Message> Messages { get; set; }
        DbSet<UserPendingMessage> UsersPendingMessages { get; set; }
    }
}
