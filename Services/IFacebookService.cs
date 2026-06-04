using System.Threading.Tasks;

namespace MaestroNotes.Services
{
    public interface IFacebookService
    {
        Task PostToFeedAsync(string message);
    }
}
