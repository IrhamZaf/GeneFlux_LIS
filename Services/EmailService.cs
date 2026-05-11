using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MailKit.Net.Smtp;
using MimeKit;
using LIS.Models;

namespace LIS.Services;

public class EmailSettings
{
    public string SmtpHost { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
    public string SmtpUser { get; set; } = "";
    public string SmtpPass { get; set; } = "";
    public string FromName { get; set; } = "Geneflux Diagnostics";
    public string FromEmail { get; set; } = "noreply@geneflux.com";
    public bool Enabled { get; set; } = false;
}

public class EmailService
{
    private readonly EmailSettings _settings;
    private readonly AppSiteSettings _site;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        IOptions<EmailSettings> settings,
        IOptions<AppSiteSettings> site,
        ILogger<EmailService> logger)
    {
        _settings = settings.Value;
        _site = site.Value;
        _logger = logger;
    }

    private string LoginPageUrl =>
        string.IsNullOrWhiteSpace(_site.PublicBaseUrl)
            ? "/account/login"
            : $"{_site.PublicBaseUrl.TrimEnd('/')}/account/login";

    /// <summary>After self-service registration approval — applicant already chose a password; do not send it by email.</summary>
    public async Task SendSelfServiceRegistrationWelcomeAsync(string recipientEmail, string fullName)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("Email disabled. Would send registration welcome to {Email}", recipientEmail);
            return;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
            message.To.Add(new MailboxAddress(fullName, recipientEmail));
            message.Subject = "Your Geneflux LIS account is active";

            var url = LoginPageUrl;
            message.Body = new TextPart("html")
            {
                Text = $@"
                <html>
                <body style='font-family: Arial, sans-serif; padding: 20px;'>
                    <div style='max-width: 600px; margin: 0 auto;'>
                        <div style='background: #6fd67f; padding: 20px; text-align: center;'>
                            <h1 style='color: white; margin: 0;'>Geneflux Diagnostics</h1>
                        </div>
                        <div style='padding: 30px; background: #f9f9f9;'>
                            <p>Dear {fullName},</p>
                            <p>Your account request has been approved. You can sign in to the Geneflux Laboratory Information System using the <strong>same password you chose</strong> when you registered.</p>
                            <table style='width: 100%; margin: 20px 0; border-collapse: collapse;'>
                                <tr><td style='padding: 6px 0;'><strong>Sign-in email:</strong></td><td>{recipientEmail}</td></tr>
                                <tr><td style='padding: 6px 0; vertical-align: top;'><strong>Website:</strong></td><td><a href=""{url}"">{url}</a></td></tr>
                            </table>
                            <p class='text-muted' style='font-size: 13px; color: #666;'>For security, passwords are never sent by email. If you forgot the password you set at registration, contact your administrator.</p>
                            <p>Best regards,<br/>Geneflux Diagnostics Team</p>
                        </div>
                    </div>
                </body>
                </html>"
            };

            using var client = new SmtpClient();
            await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, MailKit.Security.SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_settings.SmtpUser, _settings.SmtpPass);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Registration welcome email sent to {Email}", recipientEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send registration welcome email to {Email}", recipientEmail);
        }
    }

    /// <summary>After staff registration approval — sends login URL, email, and generated plaintext password (per operational requirement).</summary>
    public async Task SendStaffRegistrationApprovedCredentialsAsync(string recipientEmail, string fullName, string plaintextPassword)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("Email disabled. Would send registration credentials to {Email}", recipientEmail);
            return;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
            message.To.Add(new MailboxAddress(fullName, recipientEmail));
            message.Subject = "Your Geneflux LIS account is ready";

            var url = LoginPageUrl;
            message.Body = new TextPart("html")
            {
                Text = $@"
                <html>
                <body style='font-family: Arial, sans-serif; padding: 20px;'>
                    <div style='max-width: 600px; margin: 0 auto;'>
                        <div style='background: #6fd67f; padding: 20px; text-align: center;'>
                            <h1 style='color: white; margin: 0;'>Geneflux Diagnostics</h1>
                        </div>
                        <div style='padding: 30px; background: #f9f9f9;'>
                            <p>Dear {fullName},</p>
                            <p>Your account request has been approved. Use the credentials below to sign in to the Geneflux Laboratory Information System.</p>
                            <table style='width: 100%; margin: 20px 0; border-collapse: collapse;'>
                                <tr><td style='padding: 6px 0;'><strong>Website:</strong></td><td><a href=""{url}"">{url}</a></td></tr>
                                <tr><td style='padding: 6px 0;'><strong>Login email:</strong></td><td>{recipientEmail}</td></tr>
                                <tr><td style='padding: 6px 0;'><strong>Temporary password:</strong></td><td>{plaintextPassword}</td></tr>
                            </table>
                            <p style='font-size: 13px; color: #666;'>Please sign in and change your password after your first login.</p>
                            <p>Best regards,<br/>Geneflux Diagnostics Team</p>
                        </div>
                    </div>
                </body>
                </html>"
            };

            using var client = new SmtpClient();
            await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, MailKit.Security.SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_settings.SmtpUser, _settings.SmtpPass);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Registration credentials email sent to {Email}", recipientEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send registration credentials email to {Email}", recipientEmail);
        }
    }

    /// <summary>After a Super Admin creates a user account — password was set in the admin UI; never email plaintext passwords.</summary>
    public async Task SendAdminProvisionedAccountWelcomeAsync(string recipientEmail, string fullName)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("Email disabled. Would send admin-provisioned welcome to {Email}", recipientEmail);
            return;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
            message.To.Add(new MailboxAddress(fullName, recipientEmail));
            message.Subject = "Your Geneflux LIS account";

            var url = LoginPageUrl;
            message.Body = new TextPart("html")
            {
                Text = $@"
                <html>
                <body style='font-family: Arial, sans-serif; padding: 20px;'>
                    <div style='max-width: 600px; margin: 0 auto;'>
                        <div style='background: #6fd67f; padding: 20px; text-align: center;'>
                            <h1 style='color: white; margin: 0;'>Geneflux Diagnostics</h1>
                        </div>
                        <div style='padding: 30px; background: #f9f9f9;'>
                            <p>Dear {fullName},</p>
                            <p>An administrator created an account for you on the Geneflux Laboratory Information System. Use the <strong>password provided to you by your administrator</strong> (passwords are not sent by email).</p>
                            <table style='width: 100%; margin: 20px 0; border-collapse: collapse;'>
                                <tr><td style='padding: 6px 0;'><strong>Login email:</strong></td><td>{recipientEmail}</td></tr>
                                <tr><td style='padding: 6px 0; vertical-align: top;'><strong>Website:</strong></td><td><a href=""{url}"">{url}</a></td></tr>
                            </table>
                            <p>Best regards,<br/>Geneflux Diagnostics Team</p>
                        </div>
                    </div>
                </body>
                </html>"
            };

            using var client = new SmtpClient();
            await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, MailKit.Security.SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_settings.SmtpUser, _settings.SmtpPass);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Admin-provisioned welcome email sent to {Email}", recipientEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send admin-provisioned welcome email to {Email}", recipientEmail);
        }
    }

    public async Task SendReportNotificationAsync(string doctorEmail, string doctorName, string patientName, string referenceNumber)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("Email disabled. Would send notification to {Email} for report {Ref}", doctorEmail, referenceNumber);
            return;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
            message.To.Add(new MailboxAddress(doctorName, doctorEmail));
            message.Subject = $"New Lab Report Available - {referenceNumber}";

            message.Body = new TextPart("html")
            {
                Text = $@"
                <html>
                <body style='font-family: Arial, sans-serif; padding: 20px;'>
                    <div style='max-width: 600px; margin: 0 auto;'>
                        <div style='background: #6fd67f; padding: 20px; text-align: center;'>
                            <h1 style='color: white; margin: 0;'>Geneflux Diagnostics</h1>
                        </div>
                        <div style='padding: 30px; background: #f9f9f9;'>
                            <p>Dear {doctorName},</p>
                            <p>A new laboratory investigation test report is now available for your review.</p>
                            <table style='width: 100%; margin: 20px 0;'>
                                <tr><td><strong>Reference Number:</strong></td><td>{referenceNumber}</td></tr>
                                <tr><td><strong>Patient Name:</strong></td><td>{patientName}</td></tr>
                            </table>
                            <p>Please log in to the Geneflux LIS to view the full report.</p>
                            <p>Best regards,<br/>Geneflux Diagnostics Team</p>
                        </div>
                    </div>
                </body>
                </html>"
            };

            using var client = new SmtpClient();
            await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, MailKit.Security.SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_settings.SmtpUser, _settings.SmtpPass);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Email sent to {Email} for report {Ref}", doctorEmail, referenceNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", doctorEmail);
        }
    }

    public async Task SendReportCompletedEmailAsync(Report report)
    {
        if (report.Doctor == null || string.IsNullOrWhiteSpace(report.Doctor.Email))
            return;

        await SendReportNotificationAsync(
            report.Doctor.Email,
            report.Doctor.Name,
            report.Patient?.Name ?? "N/A",
            report.ReferenceNumber
        );
    }
}
