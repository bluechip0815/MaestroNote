using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using MaestroNotes.Configuration;
using System.Collections.Generic;

namespace MaestroNotes.Services
{
    public class FacebookService : IFacebookService
    {
        private readonly HttpClient _httpClient;
        private readonly FacebookSettings _settings;

        public FacebookService(HttpClient httpClient, IOptions<FacebookSettings> settings)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
        }

        public async Task PostToFeedAsync(string message)
        {
            if (string.IsNullOrWhiteSpace(_settings.PageId) || string.IsNullOrWhiteSpace(_settings.PageAccessToken))
            {
                throw new InvalidOperationException("Facebook PageId or PageAccessToken is not configured.");
            }

            var url = $"https://graph.facebook.com/v21.0/{_settings.PageId}/feed";

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("message", message),
                new KeyValuePair<string, string>("access_token", _settings.PageAccessToken)
            });

            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorResponse = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Facebook API Error ({response.StatusCode}): {errorResponse}");
            }
        }
    }
}
