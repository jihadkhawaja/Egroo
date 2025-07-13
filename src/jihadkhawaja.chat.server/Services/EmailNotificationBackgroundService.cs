using jihadkhawaja.chat.server.Database;
using jihadkhawaja.chat.server.Models;
using jihadkhawaja.chat.server.Services;
using jihadkhawaja.chat.shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace jihadkhawaja.chat.server.Services
{
    public class EmailNotificationBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<EmailNotificationBackgroundService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5); // Check every 5 minutes

        public EmailNotificationBackgroundService(IServiceProvider serviceProvider, ILogger<EmailNotificationBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Email notification background service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAndSendEmailNotifications();
                    await Task.Delay(_checkInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected when the service is stopping
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while checking for email notifications");
                    await Task.Delay(_checkInterval, stoppingToken);
                }
            }

            _logger.LogInformation("Email notification background service stopped");
        }

        private async Task CheckAndSendEmailNotifications()
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DataContext>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            // Check if email is configured
            if (!await emailService.IsEmailConfiguredAsync())
            {
                return; // Skip if email is not configured
            }

            try
            {
                // Get users with unread messages
                var usersWithUnreadMessages = await (from pm in dbContext.UsersPendingMessages
                                                    join u in dbContext.Users on pm.UserId equals u.Id
                                                    where !pm.DateUserReceivedOn.HasValue // Unread messages
                                                    group pm by new { pm.UserId, u } into g
                                                    select new 
                                                    {
                                                        UserId = g.Key.UserId,
                                                        User = g.Key.u,
                                                        UnreadCount = g.Count(),
                                                        OldestUnreadMessage = g.Min(pm => pm.DateCreated)
                                                    }).ToListAsync();

                foreach (var userInfo in usersWithUnreadMessages)
                {
                    await ProcessUserEmailNotification(userInfo.User, userInfo.UnreadCount, userInfo.OldestUnreadMessage ?? DateTimeOffset.UtcNow, emailService, dbContext);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for users with unread messages");
            }
        }

        private async Task ProcessUserEmailNotification(User user, int unreadCount, DateTimeOffset oldestUnreadDate, IEmailService emailService, DataContext dbContext)
        {
            try
            {
                // Load user details and notification settings
                var userWithDetails = await dbContext.Users
                    .Include(u => u.UserDetail)
                    .Include(u => u.NotificationSettings)
                    .FirstOrDefaultAsync(u => u.Id == user.Id);

                if (userWithDetails == null)
                {
                    return;
                }

                // Check if user has email and notifications enabled
                if (string.IsNullOrEmpty(userWithDetails.UserDetail?.Email))
                {
                    return; // No email address
                }

                // Get or create notification settings with default values
                var notificationSettings = userWithDetails.NotificationSettings;
                if (notificationSettings == null)
                {
                    notificationSettings = new UserNotificationSettings
                    {
                        UserId = userWithDetails.Id,
                        EmailNotificationsEnabled = true,
                        EmailNotificationDelayMinutes = 15
                    };
                    
                    dbContext.UserNotificationSettings.Add(notificationSettings);
                    await dbContext.SaveChangesAsync();
                }

                if (!notificationSettings.EmailNotificationsEnabled)
                {
                    return; // User has disabled email notifications
                }

                // Check if enough time has passed since the oldest unread message
                var delayMinutes = notificationSettings.EmailNotificationDelayMinutes;
                var notificationThreshold = oldestUnreadDate.AddMinutes(delayMinutes);
                
                if (DateTimeOffset.UtcNow < notificationThreshold)
                {
                    return; // Not enough time has passed
                }

                // Check if we've already sent a notification recently (within the last hour)
                if (notificationSettings.LastEmailNotificationSent.HasValue &&
                    DateTimeOffset.UtcNow - notificationSettings.LastEmailNotificationSent.Value < TimeSpan.FromHours(1))
                {
                    return; // Already sent notification recently
                }

                // Send email notification
                var userName = userWithDetails.UserDetail.GetDisplayName() ?? userWithDetails.Username ?? "User";
                var emailSent = await emailService.SendUnreadMessageNotificationAsync(
                    userWithDetails.UserDetail.Email, 
                    userName, 
                    unreadCount);

                if (emailSent)
                {
                    // Update last notification sent time
                    notificationSettings.LastEmailNotificationSent = DateTimeOffset.UtcNow;
                    dbContext.UserNotificationSettings.Update(notificationSettings);
                    await dbContext.SaveChangesAsync();

                    _logger.LogInformation("Email notification sent to {Email} for {UnreadCount} unread messages", 
                        userWithDetails.UserDetail.Email, unreadCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing email notification for user {UserId}", user.Id);
            }
        }
    }
}