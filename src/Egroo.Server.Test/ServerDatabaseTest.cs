using Egroo.Server.Database;
using Egroo.Server.Models;
using Egroo.Server.Repository;
using Egroo.Server.Security;
using Egroo.Server.SignalR.Services;
using ImageMagick;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;

namespace Egroo.Server.Test;

[TestClass]
public class ServerDatabaseTest
{
    [TestMethod]
    public async Task DefaultDatabaseAppliesMigrationsAndPersistsOrderedTimestamps()
    {
        string databasePath = Path.Combine(Path.GetTempPath(), $"egroo-{Guid.NewGuid():N}.db");
        var builder = WebApplication.CreateBuilder([]);
        builder.Configuration["Database:Provider"] = null;
        builder.Configuration["ConnectionStrings:DefaultConnection"] = null;
        string connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false
        }.ToString();
        builder.Configuration["ConnectionStrings:Sqlite"] = connectionString;
        ServerDatabase.Register(builder);

        var earlier = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.FromHours(2));
        var later = earlier.AddTicks(1).ToOffset(TimeSpan.FromHours(-4));
        try
        {
            await using (var app = builder.Build())
            {
                using var scope = app.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<DataContext>();
                Assert.IsTrue(db.Database.IsSqlite());
                await db.Database.MigrateAsync();
                CollectionAssert.AreEqual(db.Database.GetMigrations().ToArray(),
                    (await db.Database.GetAppliedMigrationsAsync()).ToArray());
                db.Users.AddRange(
                    new User { Username = "later", Role = "Member", DateCreated = later },
                    new User { Username = "earlier", Role = "Member", DateCreated = earlier });
                await db.SaveChangesAsync();
            }

            var options = new DbContextOptionsBuilder<SqliteDataContext>()
                .UseSqlite(connectionString)
                .Options;
            await using var reopened = new SqliteDataContext(options);
            await reopened.Database.MigrateAsync();
            var users = await reopened.Users.OrderBy(user => user.DateCreated).ToArrayAsync();
            CollectionAssert.AreEqual(new[] { "earlier", "later" }, users.Select(user => user.Username).ToArray());
            Assert.AreEqual(later.UtcTicks, users[1].DateCreated?.UtcTicks);
            Assert.AreEqual(TimeSpan.Zero, users[1].DateCreated?.Offset);
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task SendMessage_CommitsMetadataAndAllDeliveriesTogether(bool rejectSecondDelivery)
    {
        string databasePath = Path.Combine(Path.GetTempPath(), $"egroo-delivery-{Guid.NewGuid():N}.db");
        string connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false
        }.ToString();
        var options = new DbContextOptionsBuilder<SqliteDataContext>()
            .UseSqlite(connectionString)
            .Options;

        var message = new Message { ChannelId = Guid.NewGuid(), SenderId = Guid.NewGuid() };
        MessageRecipientContent[] deliveries =
        [
            new() { UserId = message.SenderId, Content = "encrypted-for-sender" },
            new() { UserId = Guid.NewGuid(), Content = "encrypted-for-offline-recipient" }
        ];
        message.RecipientContents = [.. deliveries];

        try
        {
            await using (var writer = new SqliteDataContext(options))
            {
                await writer.Database.MigrateAsync();
                if (rejectSecondDelivery)
                {
                    await writer.Database.ExecuteSqlRawAsync("""
                        CREATE TRIGGER reject_second_delivery
                        BEFORE INSERT ON userspendingmessages
                        WHEN EXISTS (SELECT 1 FROM userspendingmessages WHERE messageid = NEW.messageid)
                        BEGIN
                            SELECT RAISE(ABORT, 'Simulated failure storing a recipient delivery');
                        END;
                        """);
                }

                var repository = CreateMessageRepository(writer);
                bool sent = await repository.SendMessage(message, deliveries);
                Assert.AreEqual(!rejectSecondDelivery, sent);
            }

            await using var reopened = new SqliteDataContext(options);
            var storedMessage = await reopened.Messages.SingleOrDefaultAsync();
            var storedDeliveries = await reopened.UsersPendingMessages.ToArrayAsync();
            if (rejectSecondDelivery)
            {
                Assert.IsNull(storedMessage);
                Assert.HasCount(0, storedDeliveries);
                return;
            }

            Assert.IsNotNull(storedMessage);
            Assert.AreEqual(message.Id, storedMessage.Id);
            Assert.AreEqual(message.ReferenceId, storedMessage.ReferenceId);
            Assert.HasCount(deliveries.Length, storedDeliveries);
            foreach (var delivery in deliveries)
            {
                var stored = storedDeliveries.Single(row => row.UserId == delivery.UserId);
                Assert.AreEqual(message.Id, stored.MessageId);
                Assert.AreEqual(delivery.Content, stored.Content);
            }
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    [TestMethod]
    public async Task ReadOnlyDatabaseRegistrationDoesNotCreateMissingSqliteFile()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"egroo-{Guid.NewGuid():N}");
        string databasePath = Path.Combine(directory, "egroo.db");
        var builder = WebApplication.CreateBuilder([]);
        builder.Configuration["Database:Provider"] = "SQLite";
        builder.Configuration["ConnectionStrings:Sqlite"] = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath
        }.ToString();

        try
        {
            ServerDatabase.Register(builder, readOnly: true);
            await using var app = builder.Build();
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();

            Assert.IsFalse(await db.Database.CanConnectAsync());
            Assert.IsFalse(Directory.Exists(directory));
        }
        finally
        {
            if (File.Exists(databasePath))
                File.Delete(databasePath);
            if (Directory.Exists(directory))
                Directory.Delete(directory);
        }
    }

    [TestMethod]
    public async Task PendingMessageReplay_IsOrderedAndExcludesOtherUsersAndInactiveRows()
    {
        // This SQLite connection keeps the relational test database alive across the two contexts.
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SqliteDataContext>().UseSqlite(connection).Options;
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var newer = new Message { DateSent = now, DateCreated = now };
        var older = new Message { DateSent = now.AddMinutes(-1), DateCreated = now.AddMinutes(-1) };
        var deleted = new Message { DateSent = now, DateDeleted = now };

        await using (var writer = new SqliteDataContext(options))
        {
            await writer.Database.MigrateAsync();
            writer.Messages.AddRange(newer, older, deleted);
            writer.UsersPendingMessages.AddRange(
                new UserPendingMessage { UserId = userId, MessageId = newer.Id, Content = "newer" },
                new UserPendingMessage { UserId = userId, MessageId = older.Id, Content = "older" },
                new UserPendingMessage { UserId = Guid.NewGuid(), MessageId = older.Id, Content = "other recipient" },
                new UserPendingMessage { UserId = userId, MessageId = older.Id, Content = "acknowledged", DateUserReceivedOn = now },
                new UserPendingMessage { UserId = userId, MessageId = older.Id, Content = "removed", DateDeleted = now },
                new UserPendingMessage { UserId = userId, MessageId = deleted.Id, Content = "deleted message" });
            await writer.SaveChangesAsync();
        }

        await using var reader = new SqliteDataContext(options);
        var deliveries = await CreateMessageRepository(reader).GetPendingMessageDeliveries(userId);

        CollectionAssert.AreEqual(
            new[] { (older.Id, "older"), (newer.Id, "newer") },
            deliveries.Select(delivery => (delivery.Message.Id, delivery.Content)).ToArray());
    }

    [TestMethod]
    public async Task MarkMessageSeen_CurrentChannelMemberRecordsFirstSeenOnce()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SqliteDataContext>().UseSqlite(connection).Options;
        await using var database = new SqliteDataContext(options);
        await database.Database.MigrateAsync();
        var stored = new Message { ChannelId = Guid.NewGuid(), SenderId = Guid.NewGuid() };
        var readerId = Guid.NewGuid();
        database.Messages.Add(stored);
        database.ChannelUsers.Add(new ChannelUser { ChannelId = stored.ChannelId, UserId = readerId });
        await database.SaveChangesAsync();

        var repository = CreateMessageRepository(database);
        var first = await repository.MarkMessageSeen(readerId, stored.Id, stored.ReferenceId);
        var repeated = await repository.MarkMessageSeen(readerId, stored.Id, stored.ReferenceId);

        Assert.IsTrue(first.Changed);
        Assert.IsNotNull(first.Message?.DateSeen);
        Assert.IsFalse(repeated.Changed);
        Assert.IsNotNull(repeated.Message);
        Assert.AreEqual(first.Message.DateSeen, repeated.Message.DateSeen);
        Assert.AreEqual(first.Message.DateSeen, (await database.Messages.AsNoTracking().SingleAsync()).DateSeen);
    }

    [TestMethod]
    public async Task MarkMessageSeen_RequiresActiveChannelMembershipAndDifferentSender()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SqliteDataContext>().UseSqlite(connection).Options;
        await using var database = new SqliteDataContext(options);
        await database.Database.MigrateAsync();
        var stored = new Message { ChannelId = Guid.NewGuid(), SenderId = Guid.NewGuid() };
        var formerMemberId = Guid.NewGuid();
        database.Messages.Add(stored);
        database.ChannelUsers.AddRange(
            new ChannelUser { ChannelId = stored.ChannelId, UserId = stored.SenderId },
            new ChannelUser { ChannelId = stored.ChannelId, UserId = formerMemberId, DateDeleted = DateTimeOffset.UtcNow });
        await database.SaveChangesAsync();

        var repository = CreateMessageRepository(database);
        foreach (Guid readerId in new[] { stored.SenderId, formerMemberId, Guid.NewGuid() })
        {
            var result = await repository.MarkMessageSeen(readerId, stored.Id, stored.ReferenceId);
            Assert.IsNull(result.Message);
            Assert.IsFalse(result.Changed);
        }

        Assert.IsNull((await database.Messages.AsNoTracking().SingleAsync()).DateSeen);
    }

    [TestMethod]
    public async Task ExistingPostgreSqlConnectionSelectsPostgreSql()
    {
        var builder = WebApplication.CreateBuilder([]);
        builder.Configuration["Database:Provider"] = null;
        builder.Configuration["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=egroo";
        ServerDatabase.Register(builder);

        await using var app = builder.Build();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        Assert.IsTrue(db.Database.IsNpgsql());
    }

    [TestMethod]
    public async Task ConcurrentFirstProfileUploadsPreserveBothImages()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var gate = new FirstProfileSaveGate();
        var options = new DbContextOptionsBuilder<SqliteDataContext>().UseSqlite(connection).AddInterceptors(gate).Options;
        var user = new User { Username = "profileimages", Role = "Member" };
        await using (var setup = new SqliteDataContext(options))
        {
            await setup.Database.MigrateAsync();
            setup.Users.Add(user);
            await setup.SaveChangesAsync();
        }

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())], "TestAuth");
        var http = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } };
        UserRepository Repository(SqliteDataContext db) => new(db, http, new ConfigurationBuilder().Build(),
            new InMemoryConnectionTracker(), NullLogger<UserRepository>.Instance);

        await using var avatarDb = new SqliteDataContext(options);
        await using var coverDb = new SqliteDataContext(options);
        using var image = new MagickImage(MagickColors.SteelBlue, 32, 24);
        var encoded = Convert.ToBase64String(image.ToByteArray(MagickFormat.Png));
        var avatarUpload = Repository(avatarDb).UpdateAvatar(encoded);
        bool coverSaved;
        try
        {
            await gate.WaitForSaveAsync();
            coverSaved = await Repository(coverDb).UpdateCover(encoded);
        }
        finally
        {
            gate.Continue();
        }

        Assert.IsTrue(await avatarUpload);
        Assert.IsTrue(coverSaved);
        await using var readDb = new SqliteDataContext(options);
        var reader = Repository(readDb);
        Assert.IsNotNull(await reader.GetAvatar(user.Id));
        Assert.IsNotNull(await reader.GetCover(user.Id));
    }

    private sealed class FirstProfileSaveGate : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource _waiting = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _proceed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task WaitForSaveAsync() => _waiting.Task.WaitAsync(TimeSpan.FromSeconds(10));
        public void Continue() => _proceed.TrySetResult();

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context?.ChangeTracker.Entries<UserStorage>().Any(item => item.State == EntityState.Added) == true
                && _waiting.TrySetResult())
                await _proceed.Task.WaitAsync(cancellationToken);
            return result;
        }
    }

    private static MessageRepository CreateMessageRepository(DataContext db) => new(
        db,
        new HttpContextAccessor(),
        new ConfigurationBuilder().Build(),
        new InMemoryConnectionTracker(),
        new EncryptionService(TestServiceProvider.EncryptionKey, TestServiceProvider.EncryptionIV),
        NullLogger<MessageRepository>.Instance);
}
