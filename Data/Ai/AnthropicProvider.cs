using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

namespace MaestroNotes.Data.Ai
{
    public class AnthropicProvider : IAiProvider
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _baseUrl;

        public AnthropicProvider(HttpClient httpClient, string apiKey, string baseUrl)
        {
            _httpClient = httpClient;
            _apiKey = apiKey;
            _baseUrl = string.IsNullOrEmpty(baseUrl) ? "https://api.anthropic.com/v1" : baseUrl.TrimEnd('/');
        }

        public async Task<string> SendRequestAsync(string systemPrompt, string userPrompt, string model)
        {
            var requestBody = new
            {
                model = model,
                system = systemPrompt,
                messages = new[]
                {
                    new { role = "user", content = userPrompt }
                },
                max_tokens = 1024
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/messages");
            request.Headers.Add("x-api-key", _apiKey);
            request.Headers.Add("anthropic-version", "2023-06-01"); // Default version
            request.Content = content;

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);

            // Response structure: content[0].text
            var text = doc.RootElement
                          .GetProperty("content")[0]
                          .GetProperty("text")
                          .GetString();

            return text ?? string.Empty;
        }
    }
}
