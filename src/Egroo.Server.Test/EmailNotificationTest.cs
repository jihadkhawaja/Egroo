using jihadkhawaja.chat.server.Services;
using jihadkhawaja.chat.shared.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace Egroo.Server.Test
{
    [TestClass]
    public class EmailNotificationTest
    {
        private Mock<IConfiguration> _mockConfiguration;
        private Mock<ILogger<EmailService>> _mockLogger;
        private EmailService _emailService;

        [TestInitialize]
        public void Initialize()
        {
            _mockConfiguration = new Mock<IConfiguration>();
            _mockLogger = new Mock<ILogger<EmailService>>();

            // Setup minimal configuration
            _mockConfiguration.Setup(c => c["Email:SmtpHost"]).Returns("smtp.test.com");
            _mockConfiguration.Setup(c => c["Email:SmtpPort"]).Returns("587");
            _mockConfiguration.Setup(c => c["Email:FromEmail"]).Returns("test@test.com");
            _mockConfiguration.Setup(c => c["Email:Password"]).Returns("testpassword");
            _mockConfiguration.Setup(c => c["Email:FromName"]).Returns("Test Chat");
            _mockConfiguration.Setup(c => c["Email:UseSsl"]).Returns("true");

            _emailService = new EmailService(_mockConfiguration.Object, _mockLogger.Object);
        }

        [TestMethod]
        public async Task IsEmailConfiguredAsync_ReturnsTrue_WhenAllSettingsPresent()
        {
            // Act
            var result = await _emailService.IsEmailConfiguredAsync();

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task IsEmailConfiguredAsync_ReturnsFalse_WhenSmtpHostMissing()
        {
            // Arrange
            _mockConfiguration.Setup(c => c["Email:SmtpHost"]).Returns((string)null);

            // Act
            var result = await _emailService.IsEmailConfiguredAsync();

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task SendUnreadMessageNotificationAsync_ReturnsFalse_WhenEmailNotConfigured()
        {
            // Arrange
            _mockConfiguration.Setup(c => c["Email:SmtpHost"]).Returns((string)null);

            // Act
            var result = await _emailService.SendUnreadMessageNotificationAsync("user@test.com", "User", 5);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task SendUnreadMessageNotificationAsync_ReturnsFalse_WhenToEmailEmpty()
        {
            // Act
            var result = await _emailService.SendUnreadMessageNotificationAsync("", "User", 5);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void UserNotificationSettings_DefaultValues_AreCorrect()
        {
            // Arrange & Act
            var settings = new UserNotificationSettings
            {
                UserId = Guid.NewGuid()
            };

            // Assert
            Assert.IsTrue(settings.EmailNotificationsEnabled);
            Assert.AreEqual(15, settings.EmailNotificationDelayMinutes);
            Assert.IsNull(settings.LastEmailNotificationSent);
        }
    }
}