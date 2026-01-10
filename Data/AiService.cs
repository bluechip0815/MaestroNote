using System;
using System.Threading.Tasks;
using MaestroNotes.Data.Ai;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace MaestroNotes.Data
{
    public class AiDirigentResponseDto
    {
        public DateTime? Born { get; set; }
        public string? Note { get; set; }
    }

    public class AiOrchesterResponseDto
    {
        public DateTime? Founded { get; set; }
        public string? Note { get; set; }
    }

    public class AiService
    {
        private readonly IAiProvider _aiProvider;
        private readonly AiSettings _settings;

        public AiService(IAiProvider aiProvider, IOptions<AiSettings> settings)
        {
            _aiProvider = aiProvider;
            _settings = settings.Value;
        }

        public async Task<object> RequestAiData(string name, string itemType)
        {
            if (!_settings.Prompts.TryGetValue(itemType, out var promptSettings))
            {
                throw new ArgumentException($"No prompt configuration found for item type: {itemType}", nameof(itemType));
            }

            string userPrompt = string.Format(promptSettings.User, name);
            string systemPrompt = _settings.System;

            // Append JSON structure requirement
            string jsonStructure = "";
            Type targetType = null;

            if (itemType.Equals("Dirigent", StringComparison.OrdinalIgnoreCase))
            {
                targetType = typeof(AiDirigentResponseDto);
                jsonStructure = JsonSerializer.Serialize(new AiDirigentResponseDto { Born = DateTime.Now, Note = "Example Note" });
            }
            else if (itemType.Equals("Orchester", StringComparison.OrdinalIgnoreCase))
            {
                targetType = typeof(AiOrchesterResponseDto);
                jsonStructure = JsonSerializer.Serialize(new AiOrchesterResponseDto { Founded = DateTime.Now, Note = "Example Note" });
            }
            else
            {
                 throw new ArgumentException($"Unsupported item type for JSON serialization: {itemType}", nameof(itemType));
            }

            userPrompt += $"\n\nPlease return the response as a raw JSON object strictly matching this structure:\n{jsonStructure}";

            try
            {
                string jsonResponse = await _aiProvider.SendRequestAsync(systemPrompt, userPrompt, _settings.Model);

                // Clean up potential markdown code blocks (```json ... ```)
                jsonResponse = StripMarkdown(jsonResponse);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                return JsonSerializer.Deserialize(jsonResponse, targetType, options);
            }
            catch (Exception ex)
            {
                // In a real scenario, you might want to log this error
                throw new ApplicationException($"AI request failed: {ex.Message}", ex);
            }
        }

        private string StripMarkdown(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            var result = text.Trim();
            if (result.StartsWith("```json"))
            {
                result = result.Substring(7);
            }
            else if (result.StartsWith("```"))
            {
                result = result.Substring(3);
            }

            if (result.EndsWith("```"))
            {
                result = result.Substring(0, result.Length - 3);
            }

            return result.Trim();
        }
    }
}
