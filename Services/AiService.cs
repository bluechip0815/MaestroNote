using System;
using System.Threading.Tasks;
using MaestroNotes.Data;
using MaestroNotes.Data.Ai;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace MaestroNotes.Services
{
    public class AiKonzertResponseDto
    {
        public string? Dirigent { get; set; }
        public string? Orchester { get; set; }

        [JsonPropertyName("Komponist: Werk")]
        public string[]? KomponistWerk { get; set; }

        public string[]? Solist { get; set; }
    }

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
        private readonly MusicService _musicService;

        public AiService(IAiProvider aiProvider, IOptions<AiSettings> settings, ILogger<AiService> logger, MusicService musicService)
        {
            _aiProvider = aiProvider;
            _settings = settings.Value;
            _logger = logger;
            _musicService = musicService;
        }

        public async Task<AiKonzertResponseDto?> GetConcertPreview(string location, DateTime date)
        {
            _logger.LogInformation($"Checking concert preview for {location} on {date}");

            if (!_settings.Prompts.TryGetValue("Konzert", out var promptSettings))
            {
                _logger.LogError("No prompt configuration found for 'Konzert'");
                return null;
            }

            string userPrompt = string.Format(promptSettings.User, date.ToString("yyyy-MM-dd"), location);
            string systemPrompt = _settings.System;

            // Expected JSON structure
            var dummyDto = new AiKonzertResponseDto
            {
                Dirigent = "Name",
                Orchester = "Name",
                KomponistWerk = new[] { "Composer: Work" },
                Solist = new[] { "Name" }
            };
            string jsonStructure = JsonSerializer.Serialize(dummyDto);

            userPrompt += $"\n\nPlease return the response as a raw JSON object strictly matching this structure:\n{jsonStructure}";

            try
            {
                // Use ModelReasoning if available, otherwise fallback to default Model
                string modelToUse = !string.IsNullOrWhiteSpace(_settings.ModelReasoning) ? _settings.ModelReasoning : _settings.Model;

                string jsonResponse = await _aiProvider.SendRequestAsync(systemPrompt, userPrompt, modelToUse);
                jsonResponse = StripMarkdown(jsonResponse);

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var data = JsonSerializer.Deserialize<AiKonzertResponseDto>(jsonResponse, options);

                return data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing concert check preview");
                return null;
            }
        }

        public async Task<MusicRecord?> SaveConcertData(AiKonzertResponseDto data, string location, DateTime date)
        {
             if (data == null) return null;

             try
             {
                // Create MusicRecord
                int startYear = date.Month >= 8 ? date.Year : date.Year - 1;
                var record = new MusicRecord
                {
                    Datum = date,
                    Spielsaison = $"{startYear}/{(startYear + 1) % 100}",
                    Bezeichnung = "Konzert"
                };

                // Ort
                if (!string.IsNullOrEmpty(location))
                {
                    var ort = _musicService.GetAllOrte().FirstOrDefault(o => o.Name.Equals(location, StringComparison.OrdinalIgnoreCase));
                    if (ort == null)
                    {
                        ort = new Ort { Name = location };
                        await _musicService.AddOrt(ort);
                    }
                    record.OrtEntity = ort;
                    record.OrtId = ort.Id;
                }

                // Dirigent
                if (!string.IsNullOrEmpty(data.Dirigent))
                {
                    var dir = _musicService.GetAllDirigenten().FirstOrDefault(d => d.Name.Equals(data.Dirigent, StringComparison.OrdinalIgnoreCase));
                    if (dir == null)
                    {
                        dir = new Dirigent { Name = data.Dirigent };
                        await _musicService.AddDirigent(dir);
                    }
                    record.Dirigent = dir;
                    record.DirigentId = dir.Id;
                }

                // Orchester
                if (!string.IsNullOrEmpty(data.Orchester))
                {
                    var orch = _musicService.GetAllOrchester().FirstOrDefault(o => o.Name.Equals(data.Orchester, StringComparison.OrdinalIgnoreCase));
                    if (orch == null)
                    {
                        orch = new Orchester { Name = data.Orchester };
                        await _musicService.AddOrchester(orch);
                    }
                    record.Orchester = orch;
                    record.OrchesterId = orch.Id;
                }

                // Solisten
                if (data.Solist != null)
                {
                    foreach (var sName in data.Solist)
                    {
                        if (string.IsNullOrWhiteSpace(sName)) continue;
                        var solist = _musicService.GetAllSolisten().FirstOrDefault(s => s.Name.Equals(sName, StringComparison.OrdinalIgnoreCase));
                        if (solist == null)
                        {
                            solist = new Solist { Name = sName };
                            await _musicService.AddSolist(solist);
                        }
                        record.Solisten.Add(solist);
                    }
                }

                // Werke (Komponist: Werk)
                if (data.KomponistWerk != null)
                {
                    foreach (var kw in data.KomponistWerk)
                    {
                        if (string.IsNullOrWhiteSpace(kw)) continue;

                        string kName = "";
                        string wName = kw;

                        if (kw.Contains(":"))
                        {
                            var parts = kw.Split(new[] { ':' }, 2);
                            kName = parts[0].Trim();
                            wName = parts[1].Trim();
                        }

                        Komponist? komponist = null;
                        if (!string.IsNullOrEmpty(kName))
                        {
                            komponist = _musicService.GetAllKomponisten().FirstOrDefault(k => k.Name.Equals(kName, StringComparison.OrdinalIgnoreCase));
                            if (komponist == null)
                            {
                                komponist = new Komponist { Name = kName };
                                await _musicService.AddKomponist(komponist);
                            }
                        }

                        var werk = _musicService.GetAllWerke().FirstOrDefault(w => w.Name.Equals(wName, StringComparison.OrdinalIgnoreCase) && (komponist == null || w.Komponist == komponist));
                        if (werk == null)
                        {
                            werk = new Werk { Name = wName, Komponist = komponist };
                            await _musicService.AddWerk(werk);
                        }

                        record.Werke.Add(werk);
                    }
                }

                await _musicService.SaveDataSet(record);
                return record;
             }
             catch (Exception ex)
             {
                 _logger.LogError(ex, "Error saving concert data");
                 throw;
             }
        }

        public async Task<MusicRecord?> ExecuteConcertCheck(string location, DateTime date)
        {
            var data = await GetConcertPreview(location, date);
            if (data == null) return null;
            return await SaveConcertData(data, location, date);
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
