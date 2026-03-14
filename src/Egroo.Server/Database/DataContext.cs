using Egroo.Server.Models;
using jihadkhawaja.chat.shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace Egroo.Server.Database
{
    public class DataContext : DbContext, IDataEntities
    {
        public DataContext(DbContextOptions<DataContext> options)
            : base(options)
        {
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            base.ConfigureConventions(configurationBuilder);
            configurationBuilder.Conventions.Add(_ => new LowerCaseNamingConvention());
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // UserFeedback index (was previously [Index] attribute in the shared package)
            modelBuilder.Entity<UserFeedback>()
                .HasIndex(x => x.UserId)
                .IsUnique(false);

            // UserSecurity is an owned entity stored in its own table
            modelBuilder.Entity<User>()
                .OwnsOne(u => u.UserSecuriy, b =>
                {
                    b.ToTable("usersecurity");
                    b.WithOwner().HasForeignKey("UserId");
                    b.HasKey("UserId");
                });

            // Agent entity indexes
            modelBuilder.Entity<AgentDefinition>()
                .HasIndex(x => x.UserId)
                .IsUnique(false);

            modelBuilder.Entity<AgentKnowledge>()
                .HasIndex(x => x.AgentDefinitionId)
                .IsUnique(false);

            modelBuilder.Entity<AgentSkillDirectory>()
                .HasIndex(x => x.AgentDefinitionId)
                .IsUnique(false);

            modelBuilder.Entity<AgentTool>()
                .HasIndex(x => x.AgentDefinitionId)
                .IsUnique(false);

            modelBuilder.Entity<AgentMcpServer>()
                .HasIndex(x => x.AgentDefinitionId)
                .IsUnique(false);

            modelBuilder.Entity<AgentConversation>()
                .HasIndex(x => new { x.AgentDefinitionId, x.UserId })
                .IsUnique(false);

            modelBuilder.Entity<AgentConversationMessage>()
                .HasIndex(x => x.AgentConversationId)
                .IsUnique(false);

            modelBuilder.Entity<ChannelAgent>()
                .HasIndex(x => new { x.ChannelId, x.AgentDefinitionId })
                .IsUnique(true);

            modelBuilder.Entity<UserAgentFriend>()
                .HasIndex(x => new { x.UserId, x.AgentDefinitionId })
                .IsUnique(true);
        }

        public DbSet<User> Users { get; set; }
        public DbSet<UserFriend> UsersFriends { get; set; }
        public DbSet<Channel> Channels { get; set; }
        public DbSet<ChannelUser> ChannelUsers { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<UserPendingMessage> UsersPendingMessages { get; set; }

        // Agent entities
        public DbSet<AgentDefinition> AgentDefinitions { get; set; }
        public DbSet<AgentSkillDirectory> AgentSkillDirectories { get; set; }
        public DbSet<AgentKnowledge> AgentKnowledgeItems { get; set; }
        public DbSet<AgentTool> AgentTools { get; set; }
        public DbSet<AgentMcpServer> AgentMcpServers { get; set; }
        public DbSet<AgentConversation> AgentConversations { get; set; }
        public DbSet<AgentConversationMessage> AgentConversationMessages { get; set; }
        public DbSet<ChannelAgent> ChannelAgents { get; set; }
        public DbSet<UserAgentFriend> UserAgentFriends { get; set; }
    }

    internal class LowerCaseNamingConvention : IModelFinalizingConvention
    {
        public void ProcessModelFinalizing(IConventionModelBuilder modelBuilder, IConventionContext<IConventionModelBuilder> context)
        {
            foreach (var entity in modelBuilder.Metadata.GetEntityTypes())
            {
                var tableName = entity.GetTableName();
                if (!string.IsNullOrEmpty(tableName))
                {
                    entity.SetTableName(tableName.ToLower());
                }

                foreach (var property in entity.GetProperties())
                {
                    var columnName = property.GetColumnName();
                    if (!string.IsNullOrEmpty(columnName))
                    {
                        property.SetColumnName(columnName.ToLower());
                    }
                }
            }
        }
    }
}
