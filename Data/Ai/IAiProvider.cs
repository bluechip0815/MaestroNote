using System.Threading;
using System.Threading.Tasks;

namespace MaestroNotes.Data.Ai
{
    public interface IAiProvider
    {
        Task<string> SendRequestAsync(string systemPrompt, string userPrompt, string model, object? jsonSchema = null, CancellationToken cancellationToken = default);
    }
}
