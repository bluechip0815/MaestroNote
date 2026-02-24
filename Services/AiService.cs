using System;
using System.Threading.Tasks;
using MaestroNotes.Data.Ai;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MaestroNotes.Services
{
    public class AiDirigentResponseDto
    {
        public DateTime? Born { get; set; }
        public DateTime? Died { get; set; }
        public string? Note { get; set; }
    }

    public class AiOrchesterResponseDto
    {
        public DateTime? Founded { get; set; }
        public string? Note { get; set; }
    }

    public class AiSolistResponseDto
    {
        public DateTime? Born { get; set; }
        public DateTime? Died { get; set; }
        public string? Note { get; set; }
    }

    public class AiKomponistResponseDto
    {
        public DateTime? Born { get; set; }
        public DateTime? Died { get; set; }
        public string? Note { get; set; }
    }

    public class AiWerkResponseDto
    {
        public string? Note { get; set; }
    }

    public class AiOrtResponseDto
    {
        public string? Note { get; set; }
    }

    public class AiService
    {
        private readonly IAiProvider _aiProvider;
        private readonly AiSettings _settings;
        private readonly ILogger<AiService> _logger;

        public AiService(IAiProvider aiProvider, IOptions<AiSettings> settings, ILogger<AiService> logger)
        {
            _aiProvider = aiProvider;
            _settings = settings.Value;
            _logger = logger;
        }

        public Task ExecuteConcertCheck(string location, DateTime date)
        {
            _logger.LogInformation($"Checking concert for {location} on {date}");
            return Task.CompletedTask;
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
            Type? targetType = null;

            if (itemType.Equals("Dirigent", StringComparison.OrdinalIgnoreCase))
            {
                targetType = typeof(AiDirigentResponseDto);
                jsonStructure = JsonSerializer.Serialize(new AiDirigentResponseDto { Born = DateTime.Now, Died = DateTime.Now, Note = "Example Note" });
            }
            else if (itemType.Equals("Orchester", StringComparison.OrdinalIgnoreCase))
            {
                targetType = typeof(AiOrchesterResponseDto);
                jsonStructure = JsonSerializer.Serialize(new AiOrchesterResponseDto { Founded = DateTime.Now, Note = "Example Note" });
            }
            else if (itemType.Equals("Solist", StringComparison.OrdinalIgnoreCase))
            {
                targetType = typeof(AiSolistResponseDto);
                jsonStructure = JsonSerializer.Serialize(new AiSolistResponseDto { Born = DateTime.Now, Died = DateTime.Now, Note = "Example Note" });
            }
            else if (itemType.Equals("Komponist", StringComparison.OrdinalIgnoreCase))
            {
                targetType = typeof(AiKomponistResponseDto);
                jsonStructure = JsonSerializer.Serialize(new AiKomponistResponseDto { Born = DateTime.Now, Died = DateTime.Now, Note = "Example Note" });
            }
            else if (itemType.Equals("Werk", StringComparison.OrdinalIgnoreCase))
            {
                targetType = typeof(AiWerkResponseDto);
                jsonStructure = JsonSerializer.Serialize(new AiWerkResponseDto { Note = "Example Note" });
            }
            else if (itemType.Equals("Ort", StringComparison.OrdinalIgnoreCase))
            {
                targetType = typeof(AiOrtResponseDto);
                jsonStructure = JsonSerializer.Serialize(new AiOrtResponseDto { Note = "Example Note" });
            }
            else
            {
                 throw new ArgumentException($"Unsupported item type for JSON serialization: {itemType}", nameof(itemType));
            }

            userPrompt += $"\n\nPlease return the response as a raw JSON object strictly matching this structure:\n{jsonStructure}";

            try
            {
                _logger.LogInformation("Sending AI Request for {ItemType}: {Name}", itemType, name);
                string jsonResponse = await _aiProvider.SendRequestAsync(systemPrompt, userPrompt, _settings.Model);

                // Clean up potential markdown code blocks (```json ... ```)
                jsonResponse = StripMarkdown(jsonResponse);

                _logger.LogDebug("AI Response Raw: {JsonResponse}", jsonResponse);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                return JsonSerializer.Deserialize(jsonResponse, targetType, options) ?? "";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI request failed");
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
