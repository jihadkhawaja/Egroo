using JihadKhawaja.SignalR.Server.Chat.Models;
using Microsoft.EntityFrameworkCore;

namespace MobileChat.Server.Database
{
    public class DataContext : DbContext
    {
        protected readonly IConfiguration Configuration;

        public DataContext(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
#if DEBUG
            options.UseNpgsql(Configuration.GetConnectionString("DefaultConnection"));
#else
            options.UseNpgsql(Configuration.GetConnectionString("ProductionConnection"));
#endif
        }

        public DbSet<User> Users { get; set; }
        public DbSet<UserFriend> UsersFriends { get; set; }
        public DbSet<Channel> Channels { get; set; }
        public DbSet<ChannelUser> ChannelUsers { get; set; }
        public DbSet<Message> Messages { get; set; }
    }
}
