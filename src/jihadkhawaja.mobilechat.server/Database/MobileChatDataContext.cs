using jihadkhawaja.mobilechat.server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace jihadkhawaja.mobilechat.server.Database
{
    public abstract class MobileChatDataContext : DbContext
    {
        protected readonly IConfiguration Configuration;

        public MobileChatDataContext(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            if(string.IsNullOrWhiteSpace(MobileChatServer.DbConnectionStringKey))
            {
                throw new ArgumentException($"Key \"{nameof(MobileChatServer.DbConnectionStringKey)}\" can't be empty");
            }
            else if(string.IsNullOrWhiteSpace(Configuration.GetConnectionString(MobileChatServer.DbConnectionStringKey)))
            {
                throw new ArgumentException($"Connection string value of \"{nameof(MobileChatServer.DbConnectionStringKey)}\" is empty or doesn't exist");
            }

            switch (MobileChatServer.SelectedDatabase)
            {
                case MobileChatServer.DatabaseEnum.Postgres:
                    options.UseNpgsql(Configuration.GetConnectionString(MobileChatServer.DbConnectionStringKey), b =>
                    b.MigrationsAssembly(MobileChatServer.CurrentExecutionAssemblyName));
                    break;
                case MobileChatServer.DatabaseEnum.SqlServer:
                    options.UseSqlServer(Configuration.GetConnectionString(MobileChatServer.DbConnectionStringKey), b =>
                    b.MigrationsAssembly(MobileChatServer.CurrentExecutionAssemblyName));
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        public DbSet<User>? Users { get; set; }
        public DbSet<UserFriend>? UsersFriends { get; set; }
        public DbSet<Channel>? Channels { get; set; }
        public DbSet<ChannelUser>? ChannelUsers { get; set; }
        public DbSet<Message>? Messages { get; set; }
    }
}
