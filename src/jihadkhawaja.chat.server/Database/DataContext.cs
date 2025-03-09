﻿using jihadkhawaja.chat.shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace jihadkhawaja.chat.server.Database
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
            if (string.IsNullOrWhiteSpace(Register.ChatService.DbConnectionStringKey))
            {
                throw new ArgumentException($"Key \"{nameof(Register.ChatService.DbConnectionStringKey)}\" can't be empty");
            }
            else if (string.IsNullOrWhiteSpace(Configuration.GetConnectionString(Register.ChatService.DbConnectionStringKey)))
            {
                throw new ArgumentException($"Connection string value of \"{nameof(Register.ChatService.DbConnectionStringKey)}\" is empty or doesn't exist");
            }

            switch (Register.ChatService.SelectedDatabase)
            {
                case DatabaseEnum.Postgres:
                    options.UseNpgsql(Configuration.GetConnectionString(Register.ChatService.DbConnectionStringKey), b =>
                    b.MigrationsAssembly(Register.ChatService.CurrentExecutionAssemblyName));
                    break;
                case DatabaseEnum.SqlServer:
                    options.UseSqlServer(Configuration.GetConnectionString(Register.ChatService.DbConnectionStringKey), b =>
                    b.MigrationsAssembly(Register.ChatService.CurrentExecutionAssemblyName));
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        public DbSet<User> Users { get; set; }
        public DbSet<UserFriend> UsersFriends { get; set; }
        public DbSet<Channel> Channels { get; set; }
        public DbSet<ChannelUser> ChannelUsers { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<UserPendingMessage> UsersPendingMessages { get; set; }
    }
}
