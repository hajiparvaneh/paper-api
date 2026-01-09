namespace PaperAPI.Infrastructure.Services;

/// <summary>
/// Email template builder that creates consistent HTML emails with header and footer.
/// Uses the PaperAPI brand colors: dark background with emerald green accents.
/// </summary>
public class EmailTemplateBuilder
{
    private const string BrandColor = "#10b981"; // Emerald-500
    private const string BackgroundColor = "#020617"; // Slate-950
    private const string DarkGray = "#1e293b"; // Slate-800
    private const string TextColor = "#f8fafc"; // Foreground

    /// <summary>
    /// Wraps content with header and footer using PaperAPI brand design.
    /// </summary>
    /// <param name="content">HTML content to wrap. IMPORTANT: This content is inserted directly into the HTML template
    /// without sanitization. Callers MUST ensure that any user-provided data within the content is properly escaped
    /// using EscapeHtml() to prevent XSS vulnerabilities.</param>
    /// <param name="subject">Email subject (used in header)</param>
    /// <returns>Complete HTML email template</returns>
    public static string BuildTemplate(string content, string subject)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{EscapeHtml(subject)}</title>
    <style>
        body {{
            margin: 0;
            padding: 0;
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
            background-color: {BackgroundColor};
            color: {TextColor};
        }}
        .container {{
            max-width: 600px;
            margin: 0 auto;
            background-color: {BackgroundColor};
        }}
        .header {{
            background: linear-gradient(135deg, {DarkGray} 0%, {BackgroundColor} 100%);
            padding: 40px 20px;
            text-align: center;
            border-bottom: 2px solid {BrandColor};
        }}
        .logo {{
            font-size: 24px;
            font-weight: bold;
            color: {BrandColor};
            margin-bottom: 10px;
            letter-spacing: 0.05em;
        }}
        .header-subtitle {{
            font-size: 12px;
            color: rgba(248, 250, 252, 0.6);
            text-transform: uppercase;
            letter-spacing: 0.3em;
        }}
        .content {{
            padding: 40px 20px;
            background-color: {BackgroundColor};
        }}
        .content p {{
            margin: 0 0 16px 0;
            line-height: 1.6;
            font-size: 14px;
        }}
        .content h2 {{
            color: {BrandColor};
            font-size: 18px;
            margin: 20px 0 10px 0;
            font-weight: 600;
        }}
        .content h3 {{
            color: {TextColor};
            font-size: 16px;
            margin: 16px 0 8px 0;
            font-weight: 600;
        }}
        .content,
        .content * {{
            color: {TextColor};
        }}
        .content a {{
            color: {BrandColor};
            text-decoration: none;
        }}
        .content a:hover {{
            text-decoration: underline;
        }}
        .cta-button {{
            display: inline-block;
            background-color: {BrandColor};
            color: {BackgroundColor};
            padding: 12px 24px;
            border-radius: 6px;
            text-decoration: none;
            font-weight: 600;
            font-size: 14px;
            margin: 20px 0;
            transition: all 0.3s ease;
        }}
        .cta-button:hover {{
            opacity: 0.9;
            transform: translateY(-2px);
        }}
        .secondary-button {{
            display: inline-block;
            background-color: transparent;
            color: {BrandColor};
            padding: 12px 24px;
            border: 1px solid {BrandColor};
            border-radius: 6px;
            text-decoration: none;
            font-weight: 600;
            font-size: 14px;
            margin: 20px 10px 20px 0;
            transition: all 0.3s ease;
        }}
        .secondary-button:hover {{
            background-color: {BrandColor};
            color: {BackgroundColor};
        }}
        .divider {{
            border: none;
            border-top: 1px solid rgba(248, 250, 252, 0.1);
            margin: 30px 0;
        }}
        .footer {{
            background-color: {DarkGray};
            padding: 30px 20px;
            text-align: center;
            border-top: 1px solid rgba(248, 250, 252, 0.1);
        }}
        .footer-content {{
            font-size: 12px;
            color: rgba(248, 250, 252, 0.7);
            line-height: 1.6;
        }}
        .footer-links {{
            margin: 15px 0 0 0;
        }}
        .footer-links a {{
            color: {BrandColor};
            text-decoration: none;
            font-size: 12px;
            margin: 0 12px;
        }}
        .footer-links a:hover {{
            text-decoration: underline;
        }}
        .highlight {{
            color: {BrandColor};
            font-weight: 600;
        }}
        ul {{
            margin: 15px 0;
            padding-left: 20px;
        }}
        li {{
            margin: 8px 0;
            line-height: 1.6;
        }}
        code {{
            background-color: {DarkGray};
            padding: 2px 6px;
            border-radius: 3px;
            font-family: 'Courier New', monospace;
            font-size: 12px;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <!-- Header -->
        <div class=""header"">
            <div class=""logo"">PaperAPI</div>
            <div class=""header-subtitle"">HTML to PDF API</div>
        </div>

        <!-- Content -->
        <div class=""content"">
            {content}
        </div>

        <!-- Footer -->
        <div class=""footer"">
            <div class=""footer-content"">
                <p>© 2025 PaperAPI. All rights reserved.</p>
                <div class=""footer-links"">
                    <a href=""https://paperapi.de"">Website</a>
                    <a href=""https://paperapi.de/docs"">Documentation</a>
                    <a href=""https://paperapi.de/privacy"">Privacy Policy</a>
                </div>
            </div>
        </div>
    </div>
</body>
</html>
";
    }

    /// <summary>
    /// Escapes HTML special characters to prevent injection.
    /// Use this method to sanitize any user-provided data before including it in email content.
    /// </summary>
    public static string EscapeHtml(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }

    /// <summary>
    /// Helper to create a simple welcome email template.
    /// </summary>
    public static string BuildWelcomeTemplate(string userName)
    {
        return BuildTemplate($@"
<h2>Welcome to PaperAPI!</h2>
<p>Hello <span class=""highlight"">{EscapeHtml(userName)}</span>,</p>
<p>Thank you for signing up. You can now start generating PDFs using our EU-hosted wkhtmltopdf API.</p>
<p>Get started by:</p>
<ul>
    <li>Reading our <a href=""https://paperapi.de/docs"" style=""color: {BrandColor}; text-decoration: none;"">documentation</a></li>
    <li>Accessing your <a href=""https://paperapi.de/dashboard"" style=""color: {BrandColor}; text-decoration: none;"">dashboard</a></li>
    <li>Creating your first API key</li>
</ul>
<p>If you have any questions, feel free to reach out to us.</p>
", "Welcome to PaperAPI");
    }

    /// <summary>
    /// Helper to create a verification email template.
    /// </summary>
    public static string BuildVerificationTemplate(string verificationUrl, string userName)
    {
        return BuildTemplate($@"
<h2>Verify Your Email</h2>
<p>Hello <span class=""highlight"">{EscapeHtml(userName)}</span>,</p>
<p>Please verify your email address to complete your account setup:</p>
<p style=""text-align: center;"">
    <a href=""{EscapeHtml(verificationUrl)}"" class=""cta-button"">Verify Email</a>
</p>
<p style=""font-size: 12px; color: rgba(248, 250, 252, 0.6); margin-top: 20px;"">
    This link will expire in 24 hours. If you did not create this account, please ignore this email.
</p>
", "Verify Your Email");
    }

    /// <summary>
    /// Helper to create a password reset email template.
    /// </summary>
    public static string BuildPasswordResetTemplate(string resetUrl, string userName)
    {
        return BuildTemplate($@"
<h2>Reset Your Password</h2>
<p>Hello <span class=""highlight"">{EscapeHtml(userName)}</span>,</p>
<p>We received a request to reset your password. Click the button below to proceed:</p>
<p style=""text-align: center;"">
    <a href=""{EscapeHtml(resetUrl)}"" class=""cta-button"">Reset Password</a>
</p>
<p>If you did not request a password reset, you can safely ignore this email. Your account remains secure.</p>
<p style=""font-size: 12px; color: rgba(248, 250, 252, 0.6); margin-top: 20px;"">
    This link will expire in 2 hours.
</p>
", "Reset Your Password");
    }
}
