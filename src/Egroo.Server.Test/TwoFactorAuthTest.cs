using System.Security.Claims;
using System.Security.Cryptography;
using Egroo.Server.Database;
using Egroo.Server.Repository;
using Egroo.Server.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Egroo.Server.Test;

[TestClass]
public class TwoFactorAuthTest
{
    private const string Username = "testtotp";
    private const string Password = "SafeTestPassword1!";

    [TestMethod]
    [DataRow(59L, "287082")]
    [DataRow(1111111109L, "081804")]
    [DataRow(1111111111L, "050471")]
    [DataRow(1234567890L, "005924")]
    [DataRow(2000000000L, "279037")]
    [DataRow(20000000000L, "353130")]
    public void TotpMatchesRfc6238Sha1Vectors(long seconds, string sixDigitCode)
    {
        byte[] secret = "12345678901234567890"u8.ToArray();
        var now = DateTimeOffset.FromUnixTimeSeconds(seconds);
        Assert.AreEqual(sixDigitCode, TwoFactorCodes.CodeAt(secret, seconds / 30));
        Assert.AreEqual(seconds / 30, TwoFactorCodes.MatchTimeStep(secret, sixDigitCode, now, null));
        Assert.IsNull(TwoFactorCodes.MatchTimeStep(secret, sixDigitCode, now, seconds / 30));
        Assert.AreEqual("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ", TwoFactorCodes.FormatSecret(secret));
    }

    [TestMethod]
    public void StoredSecretIsBoundToTheAccountAndAuthenticatesTheCiphertext()
    {
        var user = Guid.NewGuid();
        byte[] secret = TwoFactorCodes.CreateSecret();
        string wrapped = TwoFactorCodes.ProtectSecret(secret, TestServiceProvider.EncryptionKey, user);
        CollectionAssert.AreEqual(secret, TwoFactorCodes.UnprotectSecret(wrapped, TestServiceProvider.EncryptionKey, user));

        Assert.Throws<CryptographicException>(() =>
            TwoFactorCodes.UnprotectSecret(wrapped, TestServiceProvider.EncryptionKey, Guid.NewGuid()));
        byte[] altered = Convert.FromBase64String(wrapped);
        altered[^1] ^= 1;
        Assert.Throws<CryptographicException>(() =>
            TwoFactorCodes.UnprotectSecret(Convert.ToBase64String(altered), TestServiceProvider.EncryptionKey, user));
    }

    [TestMethod]
    public async Task EnrollmentRecoveryAndReplaysPersistAcrossSqliteConnections()
    {
        using var test = new AuthFixture();
        await test.InitializeAsync();
        string key;
        string firstCode;
        string[] recoveryCodes;
        await using (var setup = test.Open())
        {
            var auth = test.Repository(setup, authenticated: true);
            Assert.IsFalse((await auth.GetTwoFactorStatus()).Enabled);
            Assert.IsFalse((await auth.BeginTwoFactorEnrollment("wrong-password")).Success);
            var begin = await auth.BeginTwoFactorEnrollment(Password);
            Assert.IsTrue(begin.Success);
            key = begin.SharedKey!;
            firstCode = test.CurrentCode(key);
        }

        await using (var verification = test.Open())
        {
            var auth = test.Repository(verification, authenticated: true);
            var activation = await auth.EnableTwoFactor(Password, firstCode);
            Assert.IsTrue(activation.Success, activation.Message);
            Assert.AreEqual(10, activation.RecoveryCodes!.Length);
            recoveryCodes = activation.RecoveryCodes!;
            var state = (await verification.Users.SingleAsync()).UserSecuriy!;
            Assert.AreNotEqual(key, state.ProtectedTotpSecret);
            var serialized = System.Text.Json.JsonSerializer.Serialize(await verification.Users.SingleAsync());
            Assert.IsFalse(serialized.Contains(nameof(state.ProtectedTotpSecret), StringComparison.Ordinal));
            Assert.IsFalse(state.RecoveryCodeHashes!.Contains(recoveryCodes[0], StringComparison.Ordinal));
        }

        await using (var challenge = test.Open())
        {
            var auth = test.Repository(challenge);
            var result = await auth.SignIn(Username, Password);
            Assert.IsTrue(result.RequiresTwoFactor);
            Assert.IsFalse(result.Success);
            Assert.IsNull(result.Token);
        }

        test.Advance(TimeSpan.FromSeconds(30));
        var nextCode = test.CurrentCode(key);
        await using (var first = test.Open())
        await using (var stale = test.Open())
        {
            // Both requests load the same unconsumed code before either writes.
            await first.Users.SingleAsync();
            await stale.Users.SingleAsync();
            Assert.IsTrue((await test.Repository(first).SignIn(Username, Password, nextCode)).Success);
            var replay = await test.Repository(stale).SignIn(Username, Password, nextCode);
            Assert.IsTrue(replay.RequiresTwoFactor);
            Assert.IsNull(replay.Token);
        }

        await using (var recovered = test.Open())
        await using (var staleRecovery = test.Open())
        {
            await recovered.Users.SingleAsync();
            await staleRecovery.Users.SingleAsync();
            Assert.IsFalse((await test.Repository(recovered).SignIn(Username, "wrong-password", recoveryCodes[0])).Success);
            Assert.IsTrue((await test.Repository(recovered).SignIn(Username, Password, recoveryCodes[0])).Success);
            var concurrentReuse = await test.Repository(staleRecovery).SignIn(Username, Password, recoveryCodes[0]);
            Assert.IsTrue(concurrentReuse.RequiresTwoFactor);
            Assert.IsNull(concurrentReuse.Token);
        }

        await using (var duplicate = test.Open())
        {
            var replay = await test.Repository(duplicate).SignIn(Username, Password, recoveryCodes[0]);
            Assert.IsTrue(replay.RequiresTwoFactor);
            Assert.IsNull(replay.Token);
        }

        await using (var disabling = test.Open())
        {
            var auth = test.Repository(disabling, authenticated: true);
            Assert.IsFalse((await auth.DisableTwoFactor(Password, recoveryCodes[0])).Success);
            Assert.IsTrue((await auth.DisableTwoFactor(Password, recoveryCodes[1])).Success);
        }

        await using var after = test.Open();
        var disabled = test.Repository(after, authenticated: true);
        Assert.IsFalse((await disabled.GetTwoFactorStatus()).Enabled);
        Assert.IsTrue((await disabled.SignIn(Username, Password)).Success);
        Assert.IsNull((await after.Users.SingleAsync()).UserSecuriy!.ProtectedTotpSecret);
    }

    [TestMethod]
    public async Task ExpiredEnrollmentAndFactorLockoutHaveBoundedLifetimes()
    {
        using var test = new AuthFixture();
        await test.InitializeAsync();
        string key;
        await using (var initial = test.Open())
        {
            var auth = test.Repository(initial, authenticated: true);
            key = (await auth.BeginTwoFactorEnrollment(Password)).SharedKey!;
        }

        test.Advance(TimeSpan.FromMinutes(10));
        await using (var delayed = test.Open())
        {
            var auth = test.Repository(delayed, authenticated: true);
            Assert.IsFalse((await auth.EnableTwoFactor(Password, test.CurrentCode(key))).Success);
            key = (await auth.BeginTwoFactorEnrollment(Password)).SharedKey!;
        }

        await using (var fresh = test.Open())
        {
            var auth = test.Repository(fresh, authenticated: true);
            Assert.IsTrue((await auth.EnableTwoFactor(Password, test.CurrentCode(key))).Success);
        }

        for (int attempt = 0; attempt < 5; attempt++)
        {
            await using var invalid = test.Open();
            var result = await test.Repository(invalid).SignIn(Username, Password, "invalid-factor");
            Assert.IsNull(result.Token);
        }

        test.Advance(TimeSpan.FromSeconds(30));
        await using (var locked = test.Open())
        {
            var result = await test.Repository(locked).SignIn(Username, Password, test.CurrentCode(key));
            Assert.IsNull(result.Token);
            StringAssert.Contains(result.Message, "Too many");
        }

        test.Advance(TimeSpan.FromMinutes(5));
        await using var unlocked = test.Open();
        Assert.IsTrue((await test.Repository(unlocked).SignIn(Username, Password, test.CurrentCode(key))).Success);
    }

    internal static string CodeForSecret(string base32Secret, DateTimeOffset now)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var secret = new byte[base32Secret.Length * 5 / 8];
        int bits = 0;
        int buffer = 0;
        int next = 0;
        foreach (var character in base32Secret)
        {
            buffer = (buffer << 5) | alphabet.IndexOf(character);
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                secret[next++] = (byte)(buffer >> bits);
            }
        }
        return TwoFactorCodes.CodeAt(secret, now.ToUnixTimeSeconds() / 30);
    }

    private sealed class AuthFixture : IDisposable
    {
        private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"egroo-totp-{Guid.NewGuid():N}.db");
        private readonly IServiceProvider _services = TestServiceProvider.Build();
        private readonly TestClock _clock = new();
        private Guid _userId;

        public async Task InitializeAsync()
        {
            await using var database = Open();
            await database.Database.MigrateAsync();
            var created = await Repository(database).SignUp(Username, Password);
            Assert.IsTrue(created.Success);
            _userId = created.UserId!.Value;
            Assert.IsTrue((await Repository(database).SignIn(Username, Password)).Success);
        }

        public SqliteDataContext Open() => new(new DbContextOptionsBuilder<SqliteDataContext>()
            .UseSqlite(new SqliteConnectionStringBuilder { DataSource = _databasePath, Pooling = false }.ToString())
            .Options);

        public AuthRepository Repository(DataContext context, bool authenticated = false)
        {
            var accessor = authenticated
                ? new HttpContextAccessor { HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, _userId.ToString())], "Test"))
                } }
                : new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
            return new(context, accessor, _services.GetRequiredService<IConfiguration>(),
                _services.GetRequiredService<IConnectionTracker>(), NullLogger<AuthRepository>.Instance, _clock);
        }

        public void Advance(TimeSpan delay) => _clock.Now += delay;

        public string CurrentCode(string base32Secret) => CodeForSecret(base32Secret, _clock.Now);

        public void Dispose()
        {
            (_services as IDisposable)?.Dispose();
            File.Delete(_databasePath);
        }
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.FromUnixTimeSeconds(1_790_000_000);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
