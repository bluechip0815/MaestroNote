using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace MaestroNotes.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendLoginLink(string email, string loginToken)
        {
            try
            {
                var smtpHost = _configuration["Smtp:Host"] ?? "localhost";
                var smtpPort = int.Parse(_configuration["Smtp:Port"] ?? "25");
                var smtpUser = _configuration["Smtp:User"];
                var smtpPass = _configuration["Smtp:Password"];
                var fromAddress = _configuration["Smtp:FromAddress"] ?? "noreply@maestronotes.local";
                var linkBase = _configuration["Smtp:Link"] ?? "https://localhost:7121";
                if (linkBase.EndsWith("/"))
                    linkBase = linkBase.Substring(0, linkBase.Length - 1);
                var fullLink = $"{linkBase}/auth/verify?token={loginToken}";

                using var client = new SmtpClient(smtpHost, smtpPort)
                {
                    EnableSsl = true,
                    Credentials = new NetworkCredential(smtpUser, smtpPass)
                };

                // For development/testing without real SMTP, we might want to log the link
                if (smtpHost == "localhost" || string.IsNullOrEmpty(smtpUser))
                {
                    // Fix: Ensure variable name matches parameter
                    Log.Information($"[MOCK EMAIL] To: {email}, Link: {fullLink}");
                    return;
                }

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromAddress),
                    Subject = "MaestroNotes Login Link",
                    Body = $"Here is your login link: {fullLink}",
                    IsBodyHtml = false
                };
                mailMessage.To.Add(email);

                await client.SendMailAsync(mailMessage);
                Log.Information($"Email sent to {email}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to send email");
                throw;
            }
        }

        public async Task SendEmailWithAttachment(string email, string subject, string body, string attachmentPath)
        {
            try
            {
                var smtpHost = _configuration["Smtp:Host"] ?? "localhost";
                var smtpPort = int.Parse(_configuration["Smtp:Port"] ?? "25");
                var smtpUser = _configuration["Smtp:User"];
                var smtpPass = _configuration["Smtp:Password"];
                var fromAddress = _configuration["Smtp:FromAddress"] ?? "noreply@maestronotes.local";

                using var client = new SmtpClient(smtpHost, smtpPort)
                {
                    EnableSsl = true,
                    Credentials = new NetworkCredential(smtpUser, smtpPass)
                };

                if (smtpHost == "localhost" || string.IsNullOrEmpty(smtpUser))
                {
                    Log.Information($"[MOCK EMAIL] To: {email}, Subject: {subject}, Body: {body}, Attachment: {attachmentPath}");
                    return;
                }

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromAddress),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = false
                };
                mailMessage.To.Add(email);

                if (File.Exists(attachmentPath))
                {
                    mailMessage.Attachments.Add(new Attachment(attachmentPath));
                }
                else
                {
                    Log.Warning($"Attachment not found: {attachmentPath}");
                }

                await client.SendMailAsync(mailMessage);
                Log.Information($"Email sent to {email} with attachment");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to send email with attachment");
                throw;
            }
        }
    }
}
