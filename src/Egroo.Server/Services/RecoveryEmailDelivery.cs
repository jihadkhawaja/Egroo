using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Egroo.Server.Services;

public interface IRecoveryEmailDelivery
{
    bool Available { get; }
    Task SendAsync(string address, string subject, string text, CancellationToken cancellationToken);
}

internal sealed class SmtpRecoveryEmailDelivery : IRecoveryEmailDelivery
{
    private readonly string? _host;
    private readonly int _port;
    private readonly string? _username;
    private readonly string? _password;
    private readonly MailboxAddress? _sender;
    private readonly SecureSocketOptions _tls;

    public SmtpRecoveryEmailDelivery(IConfiguration configuration)
    {
        var smtp = configuration.GetSection("RecoveryEmail:Smtp");
        _host = smtp["Host"];
        _port = smtp.GetValue("Port", 587);
        _username = smtp["Username"];
        _password = smtp["Password"];
        _tls = smtp.GetValue("ImplicitTls", false) ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        if (smtp["From"] is { } from && MailboxAddress.TryParse(from, out var sender))
            _sender = sender;
        Available = !string.IsNullOrWhiteSpace(_host) && _sender is not null && _port is >= 1 and <= 65535
            && !string.IsNullOrWhiteSpace(_username) && !string.IsNullOrWhiteSpace(_password);
    }

    public bool Available { get; }

    public async Task SendAsync(string address, string subject, string text, CancellationToken cancellationToken)
    {
        if (!Available)
            throw new InvalidOperationException("Recovery email delivery is not configured.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var message = new MimeMessage();
        message.From.Add(_sender!);
        message.To.Add(new MailboxAddress(string.Empty, address));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = text };

        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(_host!, _port, _tls, timeout.Token);
        await smtp.AuthenticateAsync(_username!, _password!, timeout.Token);
        var sent = await smtp.SendAsync(FormatOptions.Default, new MailKit.SendRequest(message), timeout.Token);
        if (sent.AcceptedRecipients.Count != 1)
            throw new InvalidOperationException("Recovery email was not accepted.");
    }
}
