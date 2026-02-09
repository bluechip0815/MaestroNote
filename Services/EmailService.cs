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

        public async Task SendLoginLink(string email, string token)
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

                var magicLink = $"{urlLink}/verify?token={token}";

                // For development/testing without real SMTP, we might want to log the link
                if (smtpHost == "localhost" || string.IsNullOrEmpty(smtpUser))
                {
                    Log.Information($"[MOCK EMAIL] To: {email}, Token: {token}");
                    Log.Information($"[MOCK EMAIL] Link: {magicLink}");
                    // In a real app, don't send if not configured properly, or maybe throw.
                    // But here we log it for testing.
                    return;
                }

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromAddress),
                    Subject = "MaestroNotes Login Link",
                    Body = $"Here is your login link: {magicLink}",
                    IsBodyHtml = false
                };
                mailMessage.To.Add(email);

                await client.SendMailAsync(mailMessage);
                Log.Information($"Email sent to {email}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to send email");
                throw; // Rethrow so the caller knows it failed
            }
        }
    }
}
