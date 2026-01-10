using System.Threading.Tasks;

namespace MaestroNotes.Data.Ai
{
    public interface IAiProvider
    {
        Task<string> SendRequestAsync(string systemPrompt, string userPrompt, string model);
    }
}
