using jihadkhawaja.chat.shared.Helpers;

namespace Egroo.Server.Test
{
    /// <summary>
    /// Tests the auth flow (sign-up / sign-in / change password) using the
    /// <c>jihadkhawaja.chat.server</c> hub backed by an in-memory EF Core database.
    /// These tests exercise <c>AuthRepository</c> which implements the
    /// <see cref="IAuth"/> interface defined in <c>jihadkhawaja.chat.shared</c>.
    /// </summary>
    [TestClass]
    public class AuthTest
    {
        // Unique DB per class so auth tests are fully isolated.
        private const string DbName = "AuthTestDb";
        private IServiceProvider _services = null!;

        [TestInitialize]
        public void Initialize()
        {
            _services = TestServiceProvider.Build(dbName: DbName);
        }

        // ── Sign-up ─────────────────────────────────────────────────────────────────

        [TestMethod]
        public async Task SignUp_WithValidCredentials_Succeeds()
        {
            using var scope = _services.CreateScope();
            var auth = scope.ServiceProvider.GetRequiredService<IAuth>();

            var result = await auth.SignUp("authuser1", "ValidP@ss1!");

            Assert.IsTrue(result.Success, $"SignUp failed: {result.Message}");
            Assert.IsNotNull(result.UserId, "Expected a UserId in the response.");
            Assert.IsNotNull(result.Token,  "Expected a JWT token in the response.");
        }

        [TestMethod]
        public async Task SignUp_DuplicateUsername_Fails()
        {
            using var scope1 = _services.CreateScope();
            var auth1 = scope1.ServiceProvider.GetRequiredService<IAuth>();
            await auth1.SignUp("dupuser", "ValidP@ss1!");

            using var scope2 = _services.CreateScope();
            var auth2 = scope2.ServiceProvider.GetRequiredService<IAuth>();
            var result = await auth2.SignUp("dupuser", "ValidP@ss1!");

            Assert.IsFalse(result.Success, "Expected failure for duplicate username.");
            StringAssert.Contains(result.Message!.ToLower(), "exist");
        }

        [TestMethod]
        public async Task SignUp_InvalidUsername_Fails()
        {
            using var scope = _services.CreateScope();
            var auth = scope.ServiceProvider.GetRequiredService<IAuth>();

            // PatternMatchHelper rejects empty / invalid usernames
            var result = await auth.SignUp("", "ValidP@ss1!");

            Assert.IsFalse(result.Success, "Expected failure for empty username.");
        }

        [TestMethod]
        public async Task SignUp_WeakPassword_Fails()
        {
            using var scope = _services.CreateScope();
            var auth = scope.ServiceProvider.GetRequiredService<IAuth>();

            var result = await auth.SignUp("authuser_weak", "123");

            Assert.IsFalse(result.Success, "Expected failure for weak password.");
        }

        // ── Sign-in ─────────────────────────────────────────────────────────────────

        [TestMethod]
        public async Task SignIn_WithCorrectCredentials_Succeeds()
        {
            using var scopeUp = _services.CreateScope();
            await scopeUp.ServiceProvider.GetRequiredService<IAuth>()
                         .SignUp("signinuser", "ValidP@ss1!");

            using var scopeIn = _services.CreateScope();
            var result = await scopeIn.ServiceProvider.GetRequiredService<IAuth>()
                                       .SignIn("signinuser", "ValidP@ss1!");

            Assert.IsTrue(result.Success, $"SignIn failed: {result.Message}");
            Assert.IsNotNull(result.Token,  "Expected a JWT token.");
            Assert.IsNotNull(result.UserId, "Expected a UserId.");
        }

        [TestMethod]
        public async Task SignIn_WithWrongPassword_Fails()
        {
            using var scopeUp = _services.CreateScope();
            await scopeUp.ServiceProvider.GetRequiredService<IAuth>()
                         .SignUp("wrongpassuser", "ValidP@ss1!");

            using var scopeIn = _services.CreateScope();
            var result = await scopeIn.ServiceProvider.GetRequiredService<IAuth>()
                                       .SignIn("wrongpassuser", "WrongPassword!");

            Assert.IsFalse(result.Success, "Expected failure for wrong password.");
        }

        [TestMethod]
        public async Task SignIn_NonExistentUser_Fails()
        {
            using var scope = _services.CreateScope();
            var result = await scope.ServiceProvider.GetRequiredService<IAuth>()
                                    .SignIn("ghost_user", "ValidP@ss1!");

            Assert.IsFalse(result.Success, "Expected failure for non-existent user.");
        }

        // ── PatternMatchHelper (jihadkhawaja.chat.shared) ───────────────────────────

        [TestMethod]
        public void PatternMatch_ValidUsername_ReturnsTrue()
            => Assert.IsTrue(PatternMatchHelper.IsValidUsername("validuser"));

        [TestMethod]
        public void PatternMatch_ValidPassword_ReturnsTrue()
            => Assert.IsTrue(PatternMatchHelper.IsValidPassword("ValidP@ss1!"));

        [TestMethod]
        public void PatternMatch_ShortPassword_ReturnsFalse()
            => Assert.IsFalse(PatternMatchHelper.IsValidPassword("ab1!"));
    }
}
