using System.Net.Http.Json;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace NotificationService.API.Services;

public interface INotificationSender
{
    /// <summary>Returns true if delivery succeeded; throws on failure for logging upstream.</summary>
    Task SendAsync(string channel, string target, string subject, string body, CancellationToken ct);
}

/// <summary>
/// Dispatches to the appropriate channel implementation based on the
/// "channel" value from the notification node config ("Email", "Slack", "Webhook").
/// </summary>
public class NotificationSender : INotificationSender
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly ILogger<NotificationSender> _logger;

    public NotificationSender(IConfiguration configuration, HttpClient httpClient, ILogger<NotificationSender> logger)
    {
        _configuration = configuration;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task SendAsync(string channel, string target, string subject, string body, CancellationToken ct)
    {
        switch (channel.ToLowerInvariant())
        {
            case "email":
                await SendEmailAsync(target, subject, body, ct);
                break;

            case "slack":
            case "webhook":
                await SendWebhookAsync(target, subject, body, ct);
                break;

            default:
                throw new NotSupportedException($"Unsupported notification channel '{channel}'");
        }
    }

    private async Task SendEmailAsync(string toAddress, string subject, string body, CancellationToken ct)
    {
        var smtpHost = _configuration["Smtp:Host"];
        if (string.IsNullOrWhiteSpace(smtpHost))
        {
            // No SMTP configured — log instead so the platform runs end-to-end in dev.
            _logger.LogInformation("[stub email] To: {To}, Subject: {Subject}, Body: {Body}", toAddress, subject, body);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_configuration["Smtp:FromAddress"] ?? "noreply@workflowplatform.local"));
        message.To.Add(MailboxAddress.Parse(toAddress));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        var port = int.TryParse(_configuration["Smtp:Port"], out var p) ? p : 587;

        await client.ConnectAsync(smtpHost, port, SecureSocketOptions.StartTls, ct);

        var username = _configuration["Smtp:Username"];
        var password = _configuration["Smtp:Password"];
        if (!string.IsNullOrWhiteSpace(username))
        {
            await client.AuthenticateAsync(username, password, ct);
        }

        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }

    private async Task SendWebhookAsync(string url, string subject, string body, CancellationToken ct)
    {
        var payload = new { subject, body };
        using var response = await _httpClient.PostAsJsonAsync(url, payload, ct);
        response.EnsureSuccessStatusCode();
    }
}
