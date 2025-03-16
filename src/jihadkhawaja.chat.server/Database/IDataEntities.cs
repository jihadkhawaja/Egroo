using jihadkhawaja.chat.server.Models;
using jihadkhawaja.chat.shared.Models;
using Microsoft.EntityFrameworkCore;

namespace jihadkhawaja.chat.server.Database
{
    public interface IDataEntities
    {
        public DbSet<User> Users { get; set; }
        public DbSet<UserFriend> UsersFriends { get; set; }
        public DbSet<Channel> Channels { get; set; }
        public DbSet<ChannelUser> ChannelUsers { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<UserPendingMessage> UsersPendingMessages { get; set; }
    }
}
