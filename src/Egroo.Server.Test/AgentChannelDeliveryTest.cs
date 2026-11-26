using Egroo.Server.Database;
using Egroo.Server.Models;
using Egroo.Server.Repository;
using Egroo.Server.Security;
using Egroo.Server.Services;
using Egroo.Server.SignalR.Hubs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Egroo.Server.Test;

[TestClass]
public class AgentChannelDeliveryTest
{
    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    public async Task AgentReply_UsesDurableEncryptedDeliveriesAndCurrentMembership(bool rejectSecondDelivery, bool agentRemoved)
    {
        using var services = (ServiceProvider)TestServiceProvider.Build(nameof(AgentChannelDeliveryTest));
        var encryption = services.GetRequiredService<EndToEndEncryptionService>();
        var storageEncryption = services.GetRequiredService<EncryptionService>();
        var tracker = services.GetRequiredService<IConnectionTracker>();
        var configuration = services.GetRequiredService<IConfiguration>();
        var loggerFactory = services.GetRequiredService<ILoggerFactory>();
        var desktopKey = encryption.GenerateAgentIdentity();
        var browserKey = encryption.GenerateAgentIdentity();
        var offlineKey = encryption.GenerateAgentIdentity();
        var agentKey = encryption.GenerateAgentIdentity();
        var databasePath = Path.Combine(Path.GetTempPath(), $"egroo-agent-delivery-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<SqliteDataContext>()
            .UseSqlite(new SqliteConnectionStringBuilder { DataSource = databasePath, Pooling = false }.ToString()).Options;
        var online = new User { Username = "online-member", Role = "Member" };
        var offline = new User
        {
            Username = "offline-member", Role = "Member",
            EncryptionPublicKey = offlineKey.PublicKey, EncryptionKeyId = offlineKey.KeyId
        };
        var former = new User { Username = "former-member", Role = "Member" };
        var channel = new Channel();
        var agent = new AgentDefinition
        {
            UserId = online.Id, Name = "Build helper", Model = "test",
            EncryptionPublicKey = agentKey.PublicKey, EncryptionKeyId = agentKey.KeyId
        };
        const string replyText = "Synthetic response";
        var reply = new Message
        {
            ChannelId = channel.Id, SenderId = online.Id, AgentDefinitionId = agent.Id,
            DisplayName = agent.Name, Content = replyText, DateSent = DateTimeOffset.UtcNow
        };
        tracker.TrackConnection(online.Id, "desktop");
        tracker.TrackConnection(online.Id, "browser");
        tracker.TrackConnection(former.Id, "former");
        var broadcasts = new List<(string[] Connections, string Method, Message Message, bool Committed)>();
        var hub = new DeliveryObserver(async (connections, method, message) =>
        {
            await using var observer = new SqliteDataContext(options);
            var committed = await observer.Messages.AnyAsync(row => row.Id == reply.Id)
                && await observer.UsersPendingMessages.CountAsync(row => row.MessageId == reply.Id) == 2
                && await observer.AgentPendingMessages.AnyAsync(row => row.MessageId == reply.Id);
            broadcasts.Add((connections.ToArray(), method, message, committed));
        });
        var agentService = new AgentChannelService(
            services.GetRequiredService<IServiceScopeFactory>(), services.GetRequiredService<AgentSkillsService>(),
            storageEncryption, encryption, services.GetRequiredService<McpClientService>(),
            hub, tracker, loggerFactory, services, loggerFactory.CreateLogger<AgentChannelService>());

        try
        {
            await using (var writer = new SqliteDataContext(options))
            {
                await writer.Database.MigrateAsync();
                writer.Users.AddRange(online, offline, former);
                writer.Channels.Add(channel);
                writer.ChannelUsers.AddRange(
                    new ChannelUser { ChannelId = channel.Id, UserId = online.Id },
                    new ChannelUser { ChannelId = channel.Id, UserId = offline.Id },
                    new ChannelUser { ChannelId = channel.Id, UserId = former.Id, DateDeleted = DateTimeOffset.UtcNow });
                writer.UserEncryptionKeys.AddRange(
                    new UserEncryptionKey { UserId = online.Id, PublicKey = desktopKey.PublicKey, KeyId = desktopKey.KeyId },
                    new UserEncryptionKey { UserId = online.Id, PublicKey = browserKey.PublicKey, KeyId = browserKey.KeyId });
                writer.AgentDefinitions.Add(agent);
                writer.ChannelAgents.Add(new ChannelAgent
                {
                    ChannelId = channel.Id, AgentDefinitionId = agent.Id, AddedByUserId = agent.UserId,
                    DateDeleted = agentRemoved ? DateTimeOffset.UtcNow : null
                });
                await writer.SaveChangesAsync();
                if (rejectSecondDelivery)
                {
                    await writer.Database.ExecuteSqlRawAsync("""
                        CREATE TRIGGER reject_second_agent_delivery
                        BEFORE INSERT ON userspendingmessages
                        WHEN EXISTS (SELECT 1 FROM userspendingmessages WHERE messageid = NEW.messageid)
                        BEGIN
                            SELECT RAISE(ABORT, 'Simulated failure storing an agent reply delivery');
                        END;
                        """);
                }

                var channels = new ChannelRepository(writer, new HttpContextAccessor(), configuration, tracker,
                    loggerFactory.CreateLogger<ChannelRepository>());
                if (rejectSecondDelivery)
                    await Assert.ThrowsExactlyAsync<DbUpdateException>(() => agentService.SaveAndBroadcastAgentMessageAsync(writer, channels, reply));
                else
                    await agentService.SaveAndBroadcastAgentMessageAsync(writer, channels, reply);
            }

            await using var reopened = new SqliteDataContext(options);
            if (rejectSecondDelivery || agentRemoved)
            {
                Assert.HasCount(0, broadcasts);
                Assert.IsFalse(await reopened.Messages.AnyAsync());
                Assert.IsFalse(await reopened.UsersPendingMessages.AnyAsync());
                Assert.IsFalse(await reopened.AgentPendingMessages.AnyAsync());
                return;
            }

            var broadcast = broadcasts.Single();
            Assert.IsTrue(broadcast.Committed);
            CollectionAssert.AreEquivalent(new[] { "desktop", "browser" }, broadcast.Connections);
            Assert.AreEqual("ReceiveMessage", broadcast.Method);
            Assert.AreEqual(agent.Id, broadcast.Message.AgentDefinitionId);
            Assert.AreEqual(agent.Name, broadcast.Message.DisplayName);
            Assert.AreEqual(replyText, Decrypt(broadcast.Message.Content, desktopKey));
            Assert.AreEqual(replyText, Decrypt(broadcast.Message.Content, browserKey));
            Assert.IsNull((await reopened.Messages.SingleAsync()).Content);

            var repository = new MessageRepository(reopened, new HttpContextAccessor(), configuration,
                tracker, storageEncryption, loggerFactory.CreateLogger<MessageRepository>());
            var replay = (await repository.GetPendingMessageDeliveries(offline.Id)).Single();
            Assert.AreEqual(reply.ReferenceId, replay.Message.ReferenceId);
            Assert.AreEqual(agent.Name, replay.Message.DisplayName);
            Assert.AreEqual(replyText, Decrypt(replay.Content, offlineKey));

            var persistedAgentEnvelope = await reopened.AgentPendingMessages.SingleAsync();
            Assert.AreEqual(agent.Id, persistedAgentEnvelope.AgentDefinitionId);
            Assert.AreEqual(replyText, Decrypt(persistedAgentEnvelope.Content, agentKey));
        }
        finally
        {
            File.Delete(databasePath);
        }

        string? Decrypt(string? payload, EndToEndEncryptionService.AgentEncryptionIdentity identity) =>
            encryption.DecryptTransportContent(payload, encryption.DecryptAgentPrivateKey(identity.EncryptedPrivateKey));
    }

    private sealed class DeliveryObserver(Func<IReadOnlyList<string>, string, Message, Task> observe) : IHubContext<ChatHub>, IHubClients
    {
        IHubClients IHubContext<ChatHub>.Clients => this;
        IGroupManager IHubContext<ChatHub>.Groups => throw new NotSupportedException();
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => new ObservedClients(connectionIds, observe);
        public IClientProxy All => throw new NotSupportedException();
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IClientProxy Client(string connectionId) => throw new NotSupportedException();
        public IClientProxy Group(string groupName) => throw new NotSupportedException();
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotSupportedException();
        public IClientProxy User(string userId) => throw new NotSupportedException();
        public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotSupportedException();

        private sealed class ObservedClients(
            IReadOnlyList<string> connectionIds, Func<IReadOnlyList<string>, string, Message, Task> observer) : IClientProxy
        {
            public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default) =>
                observer(connectionIds, method, (Message)args.Single()!);
        }
    }
}
