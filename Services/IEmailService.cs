namespace MaestroNotes.Services
{
    public interface IEmailService
    {
        Task SendLoginLink(string email, string token);
    }
}
