namespace PaperAPI.Application.Email.Examples;

/// <summary>
/// Example usage of the Email Service with HTML templates.
/// 
/// All emails are automatically wrapped with a branded header and footer
/// using PaperAPI's design (dark background with emerald green accents).
/// </summary>
public class EmailServiceUsageExample
{
    private readonly IEmailService _emailService;

    public EmailServiceUsageExample(IEmailService emailService)
    {
        _emailService = emailService;
    }

    // Example 1: Send simple email with automatic template wrapping
    public async Task SendSimpleEmailAsync(string userEmail, string subject, string content)
    {
        // The EmailService automatically wraps content with branded header and footer
        await _emailService.SendEmailAsync(
            to: userEmail,
            subject: subject,
            body: content,
            isHtml: true
        );
    }

    // Example 2: Send welcome email
    public async Task SendWelcomeEmailAsync(string userEmail, string userName)
    {
        var htmlContent = $@"
<h2>Welcome to PaperAPI!</h2>
<p>Hello <span class=""highlight"">{userName}</span>,</p>
<p>Thank you for signing up. You can now start generating PDFs using our EU-hosted wkhtmltopdf API.</p>
<p>Get started by:</p>
<ul>
    <li>Reading our <a href=""https://paperapi.de/docs"" style=""color: #10b981; text-decoration: none;"">documentation</a></li>
    <li>Accessing your <a href=""https://paperapi.de/dashboard"" style=""color: #10b981; text-decoration: none;"">dashboard</a></li>
    <li>Creating your first API key</li>
</ul>
<p>If you have any questions, feel free to reach out to us.</p>
";

        await _emailService.SendEmailAsync(
            to: userEmail,
            subject: "Welcome to PaperAPI",
            body: htmlContent,
            isHtml: true
        );
    }

    // Example 3: Send verification email
    public async Task SendVerificationEmailAsync(string userEmail, string userName, string verificationUrl)
    {
        var htmlContent = $@"
<h2>Verify Your Email</h2>
<p>Hello <span class=""highlight"">{userName}</span>,</p>
<p>Please verify your email address to complete your account setup:</p>
<p style=""text-align: center;"">
    <a href=""{verificationUrl}"" class=""cta-button"">Verify Email</a>
</p>
<p style=""font-size: 12px; color: rgba(248, 250, 252, 0.6); margin-top: 20px;"">
    This link will expire in 24 hours. If you did not create this account, please ignore this email.
</p>
";

        await _emailService.SendEmailAsync(
            to: userEmail,
            subject: "Verify Your Email",
            body: htmlContent,
            isHtml: true
        );
    }

    // Example 4: Send password reset email
    public async Task SendPasswordResetEmailAsync(string userEmail, string userName, string resetUrl)
    {
        var htmlContent = $@"
<h2>Reset Your Password</h2>
<p>Hello <span class=""highlight"">{userName}</span>,</p>
<p>We received a request to reset your password. Click the button below to proceed:</p>
<p style=""text-align: center;"">
    <a href=""{resetUrl}"" class=""cta-button"">Reset Password</a>
</p>
<p>If you did not request a password reset, you can safely ignore this email. Your account remains secure.</p>
<p style=""font-size: 12px; color: rgba(248, 250, 252, 0.6); margin-top: 20px;"">
    This link will expire in 2 hours.
</p>
";

        await _emailService.SendEmailAsync(
            to: userEmail,
            subject: "Reset Your Password",
            body: htmlContent,
            isHtml: true
        );
    }

    // Example 5: Send bulk email to multiple recipients
    public async Task SendBulkEmailAsync(IEnumerable<string> recipients, string subject, string content)
    {
        await _emailService.SendEmailAsync(
            to: recipients,
            subject: subject,
            body: content,
            isHtml: true
        );
    }

    // Example 6: Send custom HTML email
    public async Task SendCustomHtmlEmailAsync(string userEmail, string subject)
    {
        var htmlContent = @"
<h2>Custom Notification</h2>
<p>This is a custom HTML email with the PaperAPI branding:</p>
<ul>
    <li>Dark background matching the website</li>
    <li>Emerald green accents</li>
    <li>Professional header and footer</li>
</ul>
<p style=""text-align: center;"">
    <a href=""https://paperapi.de/dashboard"" class=""cta-button"">View Dashboard</a>
</p>
";

        await _emailService.SendEmailAsync(
            to: userEmail,
            subject: subject,
            body: htmlContent,
            isHtml: true
        );
    }

    // Example 7: Plain text email (no template wrapping)
    public async Task SendPlainTextEmailAsync(string userEmail, string subject, string plainText)
    {
        await _emailService.SendEmailAsync(
            to: userEmail,
            subject: subject,
            body: plainText,
            isHtml: false // No template wrapping for plain text
        );
    }
}
