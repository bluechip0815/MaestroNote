using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.Net.Http.Headers;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace MaestroNotes.Data.Ai
{
    public class OpenAiProvider : IAiProvider
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _baseUrl;
        private readonly ILogger<OpenAiProvider> _logger;
        private readonly bool _listModels;

        // Whitelist for modern "Responses" models
        private static readonly HashSet<string> _responseModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "gpt-5.2",
            "gpt-5.2-pro",
            "o1-pro"
        };

        private enum ModelEndpointType
        {
            ChatCompletions,
            Responses
        }

        public OpenAiProvider(HttpClient httpClient, string apiKey, string baseUrl, ILogger<OpenAiProvider> logger, bool listModels)
        {
            _httpClient = httpClient;
            _apiKey = apiKey;
            _baseUrl = baseUrl.TrimEnd('/');
            _logger = logger;
            _listModels = listModels;
        }

        public async Task<string> SendRequestAsync(string systemPrompt, string userPrompt, string model)
        {
            try
            {
                // Verify model availability if configured
                if (_listModels)
                {
                    await ListAvailableModelsAsync();
                }

                // Determine the correct endpoint type based on the model name
                // Optionally, could use CheckModelCapabilityAsync(model) here for dynamic checks
                var endpointType = GetModelEndpointType(model);
                _logger.LogInformation("Selected endpoint type '{EndpointType}' for model '{Model}'", endpointType, model);

                string endpointUrl;
                string requestJson;

                if (endpointType == ModelEndpointType.Responses)
                {
                    // Prepare payload for POST /v1/responses
                    var requestBody = new
                    {
                        model = model,
                        tools = new[] 
                        {
                            new { type = "web_search" }
                        },
                        instructions = systemPrompt,
                        input = new[]
                        {
                            new { role = "user", content = userPrompt }
                        }
                    };
                    requestJson = JsonSerializer.Serialize(requestBody);
                    endpointUrl = $"{_baseUrl}/responses";
                }
                else
                {
                    // Prepare payload for POST /v1/chat/completions (Legacy)
                    var requestBody = new
                    {
                        model = model,
                        messages = new[]
                        {
                            new { role = "system", content = systemPrompt },
                            new { role = "user", content = userPrompt }
                        },
                        temperature = 0.7
                    };
                    requestJson = JsonSerializer.Serialize(requestBody);
                    endpointUrl = $"{_baseUrl}/chat/completions";
                }

                var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, endpointUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                request.Content = content;

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("OpenAI API request failed with status code {StatusCode}: {ErrorContent}", response.StatusCode, errorContent);
                    response.EnsureSuccessStatusCode(); // Will throw HttpRequestException
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseJson);

                string resultText = string.Empty;

                if (endpointType == ModelEndpointType.Responses)
                {
                    // Parse response: find element with type="message", then extract content[0].text
                    if (doc.RootElement.TryGetProperty("output", out var outputElement) &&
                        outputElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in outputElement.EnumerateArray())
                        {
                            if (!item.TryGetProperty("type", out var typeElement) ||
                                typeElement.ValueKind != JsonValueKind.String ||
                                typeElement.GetString() != "message")
                            {
                                continue;
                            }

                            if (!item.TryGetProperty("content", out var contentElement) ||
                                contentElement.ValueKind != JsonValueKind.Array)
                            {
                                continue;
                            }

                            foreach (var contentItem in contentElement.EnumerateArray())
                            {
                                // optional aber empfohlen: nur Text-Blöcke nehmen
                                if (contentItem.TryGetProperty("type", out var contentType) &&
                                    contentType.ValueKind == JsonValueKind.String)
                                {
                                    var ct = contentType.GetString();
                                    if (ct != "output_text" && ct != "text")
                                        continue;
                                }

                                if (contentItem.TryGetProperty("text", out var textElement) &&
                                    textElement.ValueKind == JsonValueKind.String)
                                {
                                    resultText = textElement.GetString();
                                    if (!string.IsNullOrWhiteSpace(resultText))
                                        break;
                                }
                            }

                            if (!string.IsNullOrWhiteSpace(resultText))
                                break;
                        }
                    }
                }
                else
                {
                    // Parse response: choices[0].message.content
                    if (doc.RootElement.TryGetProperty("choices", out var choicesElement) && choicesElement.GetArrayLength() > 0)
                    {
                        var firstChoice = choicesElement[0];
                        if (firstChoice.TryGetProperty("message", out var messageElement))
                        {
                            if (messageElement.TryGetProperty("content", out var contentElement))
                            {
                                resultText = contentElement.GetString();
                            }
                        }
                    }
                }

                return resultText ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending request to OpenAI API.");
                throw; // Re-throw to let the caller handle or display generic error
            }
        }

        private ModelEndpointType GetModelEndpointType(string model)
        {
            // Simple whitelist check
            if (_responseModels.Contains(model))
            {
                return ModelEndpointType.Responses;
            }

            // Default to Chat Completions for all other models
            return ModelEndpointType.ChatCompletions;
        }

        // Variant: Check model capability via API (Requirement #4)
        // This method demonstrates how one might dynamically check the model type via GET /models
        // In a real scenario, you would cache this result.
        private async Task<ModelEndpointType> CheckModelCapabilityAsync(string model)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/models/{model}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(responseJson);

                    // Hypothetical check: if the model object has a "type" property or similar
                    // Since standard OpenAI API doesn't expose "type" clearly, this is illustrative.
                    // For example, if we knew 'responses' models had a specific capability flag:
                    // if (doc.RootElement.TryGetProperty("capabilities", out var cap) && cap.GetProperty("responses").GetBoolean()) ...

                    // Fallback to whitelist if API doesn't provide explicit type info
                    return GetModelEndpointType(model);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check model capability dynamically for {Model}", model);
            }

            // Fallback
            return GetModelEndpointType(model);
        }

        private async Task ListAvailableModelsAsync()
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/models");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseJson);

                if (doc.RootElement.TryGetProperty("data", out var dataElement) && dataElement.ValueKind == JsonValueKind.Array)
                {
                    _logger.LogInformation("Available Models:");
                    foreach (var modelElement in dataElement.EnumerateArray())
                    {
                        if (modelElement.TryGetProperty("id", out var idElement))
                        {
                            _logger.LogInformation(" - {ModelId}", idElement.GetString());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to list available models.");
            }
        }
    }
}
