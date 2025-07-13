# Email Notifications Setup

Egroo now supports email notifications for unread messages. This feature will automatically send email reminders to users when they have unread messages.

## Configuration

Add the following configuration to your `appsettings.json` file:

```json
{
  "Email": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": "587",
    "FromEmail": "your-email@gmail.com",
    "FromName": "Egroo Chat",
    "Password": "your-app-password",
    "UseSsl": "true"
  }
}
```

### Configuration Options

- **SmtpHost**: SMTP server hostname (e.g., `smtp.gmail.com` for Gmail)
- **SmtpPort**: SMTP server port (typically `587` for TLS, `465` for SSL)
- **FromEmail**: Email address that notifications will be sent from
- **FromName**: Display name for the sender (optional, defaults to "Egroo Chat")
- **Password**: SMTP authentication password (use app-specific passwords for Gmail)
- **UseSsl**: Whether to use SSL/TLS encryption (recommended: `true`)

## Email Providers

### Gmail Setup

1. Enable 2-factor authentication on your Google account
2. Generate an app-specific password:
   - Go to Google Account settings
   - Security → 2-Step Verification → App passwords
   - Select "Mail" and generate a password
3. Use the generated app password in the configuration

### Other Providers

The system supports any SMTP server. Common settings:

- **Outlook/Hotmail**: `smtp.live.com` port `587`
- **Yahoo**: `smtp.mail.yahoo.com` port `587` 
- **Custom SMTP**: Use your provider's SMTP settings

## User Notification Settings

Users can control their email notification preferences:

- **EmailNotificationsEnabled**: Whether to receive email notifications (default: `true`)
- **EmailNotificationDelayMinutes**: How long to wait before sending notifications (default: `15` minutes)

These settings are stored per user and can be modified through the user interface or API.

## How It Works

1. When a message is sent, it creates pending messages for recipients
2. The background service checks for unread messages every 5 minutes
3. If a user has unread messages and the delay time has passed, an email is sent
4. Emails are throttled to prevent spam (maximum once per hour per user)
5. Users can disable notifications or adjust the delay in their settings

## Troubleshooting

- **Emails not sending**: Check the application logs for SMTP errors
- **Configuration issues**: Verify all required email settings are present
- **Gmail authentication**: Ensure you're using an app-specific password, not your regular password
- **Firewall issues**: Ensure the server can connect to the SMTP host on the specified port

## Security Notes

- Store SMTP passwords securely (use environment variables or secure configuration providers in production)
- Use app-specific passwords instead of regular passwords where possible
- Enable SSL/TLS encryption for all email communications
- Consider using managed email services (SendGrid, AWS SES) for production environments