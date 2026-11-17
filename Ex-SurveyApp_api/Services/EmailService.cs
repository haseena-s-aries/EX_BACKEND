using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using System.Text.Encodings.Web;

namespace Ex_SurveyApp_api.Services;

public class EmailSettings
{
    public string FromEmail { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; }
    public bool EnableSSL { get; set; }
    public string SMTPSecure { get; set; } = string.Empty;
    public string SmtpUsername { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
}

public interface IEmailService
{
    Task SendAccountMessageAsync(string recipientEmail, string recipientName, string subject, string body, CancellationToken ct)
        => SendProjectDelayAsync(recipientEmail, recipientName, subject, body, ct);
    Task SendRegistrationOtpAsync(string recipientEmail, string recipientName, string otp,
        CancellationToken cancellationToken);
    Task SendProjectDelayAsync(string recipientEmail, string recipientName, string subject, string body,
        CancellationToken cancellationToken);
}

public class SmtpEmailService(IOptions<EmailSettings> options) : IEmailService
{
    private readonly EmailSettings settings = options.Value;

    public async Task SendRegistrationOtpAsync(string recipientEmail, string recipientName, string otp,
        CancellationToken cancellationToken)
    {
        var message = new MimeMessage
        {
            Subject = "Your Ex-Survey verification code",
            Body = new TextPart("html")
            {
                Text = $"<p>Hello {HtmlEncoder.Default.Encode(recipientName)},</p>" +
                       $"<p>Your Ex-Survey verification code is <strong>{otp}</strong>.</p>" +
                       "<p>This code expires in 10 minutes.</p>"
            }
        };
        await SendAsync(recipientEmail, recipientName, message, cancellationToken);
    }

    public Task SendProjectDelayAsync(string recipientEmail, string recipientName, string subject, string body,
        CancellationToken cancellationToken) =>
        SendAsync(recipientEmail, recipientName, new MimeMessage { Subject = subject, Body = new TextPart("plain") { Text = body } }, cancellationToken);

    private async Task SendAsync(string recipientEmail, string recipientName, MimeMessage message, CancellationToken cancellationToken)
    {
        ValidateSettings();
        message.From.Add(new MailboxAddress(settings.DisplayName, settings.FromEmail));
        message.To.Add(new MailboxAddress(recipientName, recipientEmail));

        var socketOptions = settings.EnableSSL
            ? settings.SmtpPort == 465
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTls
            : SecureSocketOptions.None;

        using var client = new MailKit.Net.Smtp.SmtpClient();
        await client.ConnectAsync(settings.SmtpHost, settings.SmtpPort, socketOptions, cancellationToken);
        await client.AuthenticateAsync(settings.SmtpUsername, settings.SmtpPassword, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        // SMTP acceptance is already confirmed; a disconnect error must not mark it failed.
        try { await client.DisconnectAsync(true, cancellationToken); }
        catch (IOException) { }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private void ValidateSettings()
    {
        if (string.IsNullOrWhiteSpace(settings.FromEmail) ||
            string.IsNullOrWhiteSpace(settings.SmtpHost) ||
            settings.SmtpPort is < 1 or > 65535 ||
            string.IsNullOrWhiteSpace(settings.SmtpUsername) ||
            string.IsNullOrWhiteSpace(settings.SmtpPassword))
            throw new InvalidOperationException("EmailSettings SMTP configuration is incomplete.");
    }
}
