namespace MaestroNotes.Services
{
    public interface IEmailService
    {
        Task SendLoginLink(string email, string token);
        Task SendEmailWithAttachment(string email, string subject, string body, string attachmentPath);
    }
}
