using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

namespace MaestroNotes.Data.Ai
{
    public class GeminiProvider : IAiProvider
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _baseUrl;

        public GeminiProvider(HttpClient httpClient, string apiKey, string baseUrl)
        {
            _httpClient = httpClient;
            _apiKey = apiKey;
            // Ensure base URL points to the correct endpoint structure or default to Google's API
            // Example default: https://generativelanguage.googleapis.com/v1beta
            _baseUrl = string.IsNullOrEmpty(baseUrl) ? "https://generativelanguage.googleapis.com/v1beta" : baseUrl.TrimEnd('/');
        }

        public async Task<string> SendRequestAsync(string systemPrompt, string userPrompt, string model, object? jsonSchema = null)
        {
            // Gemini API structure
            // URL: https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = systemPrompt + "\n" + userPrompt }
                        }
                    }
                }
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var url = $"{_baseUrl}/models/{model}:generateContent?key={_apiKey}";
            var response = await _httpClient.PostAsync(url, content);

            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);

            // Navigate to candidates[0].content.parts[0].text
            // Note: Structure validation should be robust in production
            try
            {
                var text = doc.RootElement
                              .GetProperty("candidates")[0]
                              .GetProperty("content")
                              .GetProperty("parts")[0]
                              .GetProperty("text")
                              .GetString();
                return text ?? string.Empty;
            }
            catch
            {
                return responseJson; // Fallback or throw
            }
        }
    }
}
