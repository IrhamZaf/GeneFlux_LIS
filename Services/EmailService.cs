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
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailSettings> settings, ILogger<EmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
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
