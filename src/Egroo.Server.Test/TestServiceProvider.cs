using Egroo.Server.Database;
using Egroo.Server.Repository;
using Egroo.Server.Security;
using jihadkhawaja.chat.server.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Egroo.Server.Test
{
    /// <summary>
    /// Builds an <see cref="IServiceProvider"/> wired to an in-memory EF Core database.
    /// Each unique <paramref name="dbName"/> maps to an isolated in-memory store.
    /// Pass <paramref name="authenticatedUserId"/> to simulate a signed-in HTTP context.
    /// </summary>
    public static class TestServiceProvider
    {
        // 32-char AES key and 16-char IV used across all tests
        public const string EncryptionKey = "TestEncryptKey_32BytesLong!!!!!!";
        public const string EncryptionIV  = "TestEncryptIV16!";
        public const string JwtSecret     = "super-secret-test-jwt-key-for-egroo-library-tests-32+!";

        public static IServiceProvider Build(string dbName = "EgrooTestDb", Guid? authenticatedUserId = null)
        {
            var services = new ServiceCollection();

            // ── Configuration ──────────────────────────────────────────────────────────
            IConfiguration config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Secrets:Jwt"]    = JwtSecret,
                    ["Encryption:Key"] = EncryptionKey,
                    ["Encryption:IV"]  = EncryptionIV,
                })
                .Build();

            services.AddSingleton<IConfiguration>(config);

            // ── In-memory EF Core ───────────────────────────────────────────────────────
            services.AddDbContext<DataContext>(options =>
                options.UseInMemoryDatabase(dbName));

            // ── AES Encryption ──────────────────────────────────────────────────────────
            services.AddSingleton(new EncryptionService(EncryptionKey, EncryptionIV));

            // ── HTTP context (real auth claims or anonymous) ────────────────────────────
            if (authenticatedUserId.HasValue)
            {
                services.AddSingleton<IHttpContextAccessor>(
                    _ => CreateAuthenticatedAccessor(authenticatedUserId.Value));
            }
            else
            {
                services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            }

            // ── jihadkhawaja.chat.server — InMemoryConnectionTracker ───────────────────
            services.AddSingleton<IConnectionTracker, InMemoryConnectionTracker>();

            // ── Egroo.Server repositories (implement the shared interfaces) ────────────
            services.AddScoped<IAuth, AuthRepository>();
            services.AddScoped<IChannel, ChannelRepository>();
            services.AddScoped<IMessageRepository, MessageRepository>();
            services.AddScoped<IUser, UserRepository>();

            // ── Logging ────────────────────────────────────────────────────────────────
            services.AddLogging(lb => lb.AddConsole());

            return services.BuildServiceProvider();
        }

        // ── Helpers ────────────────────────────────────────────────────────────────────

        private static IHttpContextAccessor CreateAuthenticatedAccessor(Guid userId)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId.ToString()),
                new(ClaimTypes.Name, "testuser"),
                new(ClaimTypes.Role, "Member"),
            };
            var identity  = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);
            var context   = new DefaultHttpContext { User = principal };

            return new HttpContextAccessor { HttpContext = context };
        }
    }
}
