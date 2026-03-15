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
        DbSet<AgentPendingMessage> AgentPendingMessages { get; set; }

        // Agent entities
        DbSet<AgentDefinition> AgentDefinitions { get; set; }
        DbSet<AgentKnowledge> AgentKnowledgeItems { get; set; }
        DbSet<AgentTool> AgentTools { get; set; }
        DbSet<AgentConversation> AgentConversations { get; set; }
        DbSet<AgentConversationMessage> AgentConversationMessages { get; set; }
        DbSet<ChannelAgent> ChannelAgents { get; set; }
        DbSet<UserAgentFriend> UserAgentFriends { get; set; }
        DbSet<UserEncryptionKey> UserEncryptionKeys { get; set; }
    }
}
