using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace jihadkhawaja.chat.server.Services
{
    public interface IEmailService
    {
        Task<bool> SendUnreadMessageNotificationAsync(string toEmail, string userName, int unreadCount);
        Task<bool> IsEmailConfiguredAsync();
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<bool> IsEmailConfiguredAsync()
        {
            var smtpHost = _configuration["Email:SmtpHost"];
            var smtpPort = _configuration["Email:SmtpPort"];
            var fromEmail = _configuration["Email:FromEmail"];
            var password = _configuration["Email:Password"];

            return !string.IsNullOrEmpty(smtpHost) && 
                   !string.IsNullOrEmpty(smtpPort) && 
                   !string.IsNullOrEmpty(fromEmail) && 
                   !string.IsNullOrEmpty(password);
        }

        public async Task<bool> SendUnreadMessageNotificationAsync(string toEmail, string userName, int unreadCount)
        {
            try
            {
                if (!await IsEmailConfiguredAsync())
                {
                    _logger.LogWarning("Email configuration is not complete. Cannot send notifications.");
                    return false;
                }

                if (string.IsNullOrEmpty(toEmail))
                {
                    _logger.LogWarning("Recipient email is empty. Cannot send notification.");
                    return false;
                }

                var smtpHost = _configuration["Email:SmtpHost"];
                var smtpPort = int.Parse(_configuration["Email:SmtpPort"]!);
                var fromEmail = _configuration["Email:FromEmail"];
                var fromName = _configuration["Email:FromName"] ?? "Egroo Chat";
                var password = _configuration["Email:Password"];
                var useSsl = bool.Parse(_configuration["Email:UseSsl"] ?? "true");

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(fromName, fromEmail));
                message.To.Add(new MailboxAddress(userName, toEmail));
                message.Subject = unreadCount == 1 
                    ? "You have 1 unread message"
                    : $"You have {unreadCount} unread messages";

                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = $@"
                        <html>
                        <body>
                            <h2>Hello {userName},</h2>
                            <p>You have <strong>{unreadCount}</strong> unread message{(unreadCount > 1 ? "s" : "")} waiting for you on Egroo.</p>
                            <p>Sign in to your account to read your messages.</p>
                            <br>
                            <p>Best regards,<br>Egroo Team</p>
                        </body>
                        </html>",
                    TextBody = $@"
                        Hello {userName},

                        You have {unreadCount} unread message{(unreadCount > 1 ? "s" : "")} waiting for you on Egroo.

                        Sign in to your account to read your messages.

                        Best regards,
                        Egroo Team"
                };

                message.Body = bodyBuilder.ToMessageBody();

                using var client = new SmtpClient();
                
                if (useSsl)
                {
                    await client.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.SslOnConnect);
                }
                else
                {
                    await client.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.StartTls);
                }

                if (!string.IsNullOrEmpty(password))
                {
                    await client.AuthenticateAsync(fromEmail, password);
                }

                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation("Email notification sent successfully to {Email} for {UnreadCount} unread messages", toEmail, unreadCount);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email notification to {Email}", toEmail);
                return false;
            }
        }
    }
}