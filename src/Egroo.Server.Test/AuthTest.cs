using jihadkhawaja.chat.shared.Helpers;
using Microsoft.AspNetCore.Http;

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
        private string _dbName = null!;
        private IServiceProvider _services = null!;
        public TestContext TestContext { get; set; } = null!;

        [TestInitialize]
        public void Initialize()
        {
            _dbName = $"AuthTestDb_{TestContext.TestName}_{Guid.NewGuid():N}";
            _services = TestServiceProvider.Build(dbName: _dbName);
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

        // ── Change password ──────────────────────────────────────────────────────

        [TestMethod]
        public async Task ChangePassword_WithCorrectOldPassword_Succeeds()
        {
            using var signUpScope = _services.CreateScope();
            var auth = signUpScope.ServiceProvider.GetRequiredService<IAuth>();
            var signUp = await auth.SignUp("changepwuser", "ValidP@ss1!");
            Assert.IsTrue(signUp.Success, $"SignUp failed: {signUp.Message}");

            var authenticatedServices = TestServiceProvider.Build(dbName: _dbName, authenticatedUserId: signUp.UserId);
            using var scope = authenticatedServices.CreateScope();
            var authSvc = scope.ServiceProvider.GetRequiredService<IAuth>();

            var result = await authSvc.ChangePassword("ValidP@ss1!", "NewValidP@ss2!");
            Assert.IsTrue(result.Success, $"ChangePassword failed: {result.Message}");
        }

        [TestMethod]
        public async Task ChangePassword_WithWrongOldPassword_Fails()
        {
            using var signUpScope = _services.CreateScope();
            var auth = signUpScope.ServiceProvider.GetRequiredService<IAuth>();
            var signUp = await auth.SignUp("changepwwrong", "ValidP@ss1!");
            Assert.IsTrue(signUp.Success, $"SignUp failed: {signUp.Message}");

            var authenticatedServices = TestServiceProvider.Build(dbName: _dbName, authenticatedUserId: signUp.UserId);
            using var scope = authenticatedServices.CreateScope();
            var authSvc = scope.ServiceProvider.GetRequiredService<IAuth>();

            var result = await authSvc.ChangePassword("WrongP@ss1!", "NewValidP@ss2!");
            Assert.IsFalse(result.Success, "Expected failure for wrong old password.");
        }

        [TestMethod]
        public async Task ChangePassword_Unauthenticated_Fails()
        {
            using var scope = _services.CreateScope();
            var auth = scope.ServiceProvider.GetRequiredService<IAuth>();

            var result = await auth.ChangePassword("ValidP@ss1!", "NewValidP@ss2!");
            Assert.IsFalse(result.Success, "Expected failure for unauthenticated user.");
        }

        [TestMethod]
        public async Task SignIn_VerifiesNewPasswordAfterChange()
        {
            using var signUpScope = _services.CreateScope();
            var auth = signUpScope.ServiceProvider.GetRequiredService<IAuth>();
            var signUp = await auth.SignUp("changepwverify", "ValidP@ss1!");
            Assert.IsTrue(signUp.Success);

            var authenticatedServices = TestServiceProvider.Build(dbName: _dbName, authenticatedUserId: signUp.UserId);
            using var changeScope = authenticatedServices.CreateScope();
            var changePw = await changeScope.ServiceProvider.GetRequiredService<IAuth>()
                .ChangePassword("ValidP@ss1!", "NewValidP@ss2!");
            Assert.IsTrue(changePw.Success);

            using var signInScope = _services.CreateScope();
            var result = await signInScope.ServiceProvider.GetRequiredService<IAuth>()
                .SignIn("changepwverify", "NewValidP@ss2!");
            Assert.IsTrue(result.Success, $"SignIn with new password failed: {result.Message}");
        }

        // ── RefreshSession ──────────────────────────────────────────────────────────

        [TestMethod]
        public async Task RefreshSession_WithoutAuthHeader_Fails()
        {
            using var scope = _services.CreateScope();
            var auth = scope.ServiceProvider.GetRequiredService<IAuth>();

            var result = await auth.RefreshSession();
            Assert.IsFalse(result.Success, "Expected failure without auth header.");
        }

        // ── SignUp case-insensitivity ───────────────────────────────────────────────

        [TestMethod]
        public async Task SignUp_CaseInsensitiveUsername_Treated_AsLowercase()
        {
            using var scope = _services.CreateScope();
            var auth = scope.ServiceProvider.GetRequiredService<IAuth>();

            var result = await auth.SignUp("UPPERUSER", "ValidP@ss1!");
            Assert.IsTrue(result.Success);

            using var dupScope = _services.CreateScope();
            var dupResult = await dupScope.ServiceProvider.GetRequiredService<IAuth>()
                .SignUp("upperuser", "ValidP@ss1!");
            Assert.IsFalse(dupResult.Success, "Expected duplicate for same lowercase username.");
        }

        [TestMethod]
        public async Task SignIn_CaseInsensitive_Succeeds()
        {
            using var signUpScope = _services.CreateScope();
            await signUpScope.ServiceProvider.GetRequiredService<IAuth>()
                .SignUp("caseuser", "ValidP@ss1!");

            using var signInScope = _services.CreateScope();
            var result = await signInScope.ServiceProvider.GetRequiredService<IAuth>()
                .SignIn("CASEUSER", "ValidP@ss1!");
            Assert.IsTrue(result.Success, "SignIn should be case-insensitive.");
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

        [TestMethod]
        public void PatternMatch_ValidEmail_ReturnsTrue()
            => Assert.IsTrue(PatternMatchHelper.IsValidEmail("test@example.com"));

        [TestMethod]
        public void PatternMatch_InvalidEmail_ReturnsFalse()
            => Assert.IsFalse(PatternMatchHelper.IsValidEmail("not-an-email"));

        [TestMethod]
        public void PatternMatch_EmptyEmail_ReturnsFalse()
            => Assert.IsFalse(PatternMatchHelper.IsValidEmail(""));

        [TestMethod]
        public void PatternMatch_ValidDisplayName_ReturnsTrue()
            => Assert.IsTrue(PatternMatchHelper.IsValidDisplayName("User_123"));

        [TestMethod]
        public void PatternMatch_InvalidDisplayName_TooShort_ReturnsFalse()
            => Assert.IsFalse(PatternMatchHelper.IsValidDisplayName("ab"));

        [TestMethod]
        public void PatternMatch_InvalidDisplayName_SpecialChars_ReturnsFalse()
            => Assert.IsFalse(PatternMatchHelper.IsValidDisplayName("user@name"));

        [TestMethod]
        public void PatternMatch_TooLongUsername_ReturnsFalse()
            => Assert.IsFalse(PatternMatchHelper.IsValidUsername("verylongusername_"));

        [TestMethod]
        public void PatternMatch_PasswordNoUppercase_ReturnsFalse()
            => Assert.IsFalse(PatternMatchHelper.IsValidPassword("validp@ss1!"));

        [TestMethod]
        public void PatternMatch_PasswordNoLowercase_ReturnsFalse()
            => Assert.IsFalse(PatternMatchHelper.IsValidPassword("VALIDP@SS1!"));

        [TestMethod]
        public void PatternMatch_PasswordNoDigit_ReturnsFalse()
            => Assert.IsFalse(PatternMatchHelper.IsValidPassword("ValidP@ss!!"));

        [TestMethod]
        public void PatternMatch_PasswordNoSpecialChar_ReturnsFalse()
            => Assert.IsFalse(PatternMatchHelper.IsValidPassword("ValidPass1"));

        [TestMethod]
        public async Task UpdateEncryptionKey_PersistsOnPrivateProfile()
        {
            using var signUpScope = _services.CreateScope();
            var auth = signUpScope.ServiceProvider.GetRequiredService<IAuth>();
            var signUp = await auth.SignUp("encprofile", "ValidP@ss1!");
            Assert.IsTrue(signUp.Success, $"SignUp failed: {signUp.Message}");

            var authenticatedServices = TestServiceProvider.Build(dbName: _dbName, authenticatedUserId: signUp.UserId);

            using var updateScope = authenticatedServices.CreateScope();
            var userRepo = updateScope.ServiceProvider.GetRequiredService<IUser>();
            bool updated = await userRepo.UpdateEncryptionKey("public-key-material", "fingerprint-1234");
            Assert.IsTrue(updated, "Expected encryption key update to succeed.");

            using var readScope = authenticatedServices.CreateScope();
            var profile = await readScope.ServiceProvider.GetRequiredService<IUser>().GetUserPrivateDetails();

            Assert.IsNotNull(profile, "Expected a private user profile.");
            Assert.AreEqual("public-key-material", profile.EncryptionPublicKey);
            Assert.AreEqual("fingerprint-1234", profile.EncryptionKeyId);
            Assert.IsNotNull(profile.EncryptionKeyUpdatedOn);
        }

        // ── RefreshSession with Bearer token ────────────────────────────────────────

        [TestMethod]
        public async Task RefreshSession_WithValidBearerToken_Succeeds()
        {
            using var signUpScope = _services.CreateScope();
            var auth = signUpScope.ServiceProvider.GetRequiredService<IAuth>();
            var signUp = await auth.SignUp("refreshuser", "ValidP@ss1!");
            Assert.IsTrue(signUp.Success);
            Assert.IsNotNull(signUp.Token);

            var authenticatedServices = TestServiceProvider.Build(dbName: _dbName, authenticatedUserId: signUp.UserId);
            // Set the Authorization header with the JWT token
            var accessor = authenticatedServices.GetRequiredService<IHttpContextAccessor>();
            accessor.HttpContext!.Request.Headers["Authorization"] = $"Bearer {signUp.Token}";

            using var refreshScope = authenticatedServices.CreateScope();
            var result = await refreshScope.ServiceProvider.GetRequiredService<IAuth>().RefreshSession();

            Assert.IsTrue(result.Success, $"RefreshSession failed: {result.Message}");
            Assert.IsNotNull(result.Token, "Expected a new JWT token.");
            Assert.IsNotNull(result.UserId);
        }

        [TestMethod]
        public async Task RefreshSession_WithMalformedAuthorizationHeader_Fails()
        {
            using var signUpScope = _services.CreateScope();
            var auth = signUpScope.ServiceProvider.GetRequiredService<IAuth>();
            var signUp = await auth.SignUp("refreshmalformed", "ValidP@ss1!");
            Assert.IsTrue(signUp.Success);

            var authenticatedServices = TestServiceProvider.Build(dbName: _dbName, authenticatedUserId: signUp.UserId);
            // Set a malformed Authorization header (not "Bearer <token>")
            var accessor = authenticatedServices.GetRequiredService<IHttpContextAccessor>();
            accessor.HttpContext!.Request.Headers["Authorization"] = "Basic some-credentials";

            using var refreshScope = authenticatedServices.CreateScope();
            var result = await refreshScope.ServiceProvider.GetRequiredService<IAuth>().RefreshSession();

            Assert.IsFalse(result.Success);
            StringAssert.Contains(result.Message, "Invalid Authorization header format");
        }

        [TestMethod]
        public async Task RefreshSession_WithInvalidToken_Fails()
        {
            using var signUpScope = _services.CreateScope();
            var auth = signUpScope.ServiceProvider.GetRequiredService<IAuth>();
            var signUp = await auth.SignUp("refreshinvalid", "ValidP@ss1!");
            Assert.IsTrue(signUp.Success);

            var authenticatedServices = TestServiceProvider.Build(dbName: _dbName, authenticatedUserId: signUp.UserId);
            // Set a completely invalid token that can't be parsed as JWT
            var accessor = authenticatedServices.GetRequiredService<IHttpContextAccessor>();
            accessor.HttpContext!.Request.Headers["Authorization"] = "Bearer not-a-jwt-token";

            using var refreshScope = authenticatedServices.CreateScope();
            var result = await refreshScope.ServiceProvider.GetRequiredService<IAuth>().RefreshSession();

            Assert.IsFalse(result.Success);
            StringAssert.Contains(result.Message, "Invalid token");
        }
    }
}
