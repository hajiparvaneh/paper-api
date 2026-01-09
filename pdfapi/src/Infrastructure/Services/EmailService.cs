using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using PaperAPI.Application.Email;
using PaperAPI.Infrastructure.Options;

namespace PaperAPI.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailOptions> options, ILogger<EmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(string to, string subject, string body, bool isHtml = true, CancellationToken cancellationToken = default)
    {
        await SendEmailAsync(new[] { to }, subject, body, isHtml, cancellationToken);
    }

    public async Task SendEmailAsync(IEnumerable<string> to, string subject, string body, bool isHtml = true, CancellationToken cancellationToken = default)
    {
        try
        {
            using var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_options.FromDisplayName, _options.FromAddress));

            foreach (var recipient in to)
            {
                message.To.Add(MailboxAddress.Parse(recipient));
            }

            message.Subject = subject;

            // Wrap content in email template if HTML
            var emailBody = isHtml ? EmailTemplateBuilder.BuildTemplate(body, subject) : body;
            message.Body = new TextPart(isHtml ? "html" : "plain") { Text = emailBody };

            using var client = new SmtpClient();

            _logger.LogInformation("Connecting to SMTP server {SmtpHost}:{SmtpPort} (Mode: {Mode})", _options.SmtpHost, _options.SmtpPort,
                _options.UseSsl ? "SSL" : (_options.UseStartTls ? "STARTTLS" : "PLAIN"));

            // Connect to the SMTP server
            if (_options.SkipCertificateValidation)
            {
                _logger.LogWarning("TLS certificate validation is DISABLED for SMTP (testing only). Do not use in production.");
                client.ServerCertificateValidationCallback = (s, c, h, e) => true;
            }
            if (_options.UseSsl)
            {
                await client.ConnectAsync(_options.SmtpHost, _options.SmtpPort, SecureSocketOptions.SslOnConnect, cancellationToken);
            }
            else if (_options.UseStartTls)
            {
                await client.ConnectAsync(_options.SmtpHost, _options.SmtpPort, SecureSocketOptions.StartTls, cancellationToken);
            }
            else
            {
                await client.ConnectAsync(_options.SmtpHost, _options.SmtpPort, SecureSocketOptions.None, cancellationToken);
            }

            // Authenticate if credentials are provided
            if (!string.IsNullOrEmpty(_options.SmtpUsername) && !string.IsNullOrEmpty(_options.SmtpPassword))
            {
                _logger.LogDebug("Authenticating with SMTP server. Server mechanisms: {Mechanisms}", string.Join(",", client.AuthenticationMechanisms));
                await client.AuthenticateAsync(_options.SmtpUsername, _options.SmtpPassword, cancellationToken);
            }

            // Send the message
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("Email sent successfully to {Recipients}", string.Join(", ", to));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Recipients}", string.Join(", ", to));
            throw;
        }
    }
}
