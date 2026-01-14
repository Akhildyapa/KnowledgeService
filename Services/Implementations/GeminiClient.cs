using System.Collections.Generic;
using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading;
using KnowledgeService.Models.DTOs;
using KnowledgeService.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace KnowledgeService.Services.Implementations;

public class GeminiClient : IGeminiClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GeminiClient> _logger;

    public GeminiClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<GeminiClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AiEnrichmentResponse> EnrichKnowledgeNoteAsync(
        string title,
        string content,
        CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration["Gemini:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("Gemini API key not configured, returning mock response");
            return GetMockResponse(title, content);
        }

        try
        {
            var prompt = $@"Analyze this knowledge note and return ONLY a valid JSON object with this exact structure:

{{
  ""summary"": ""2-3 sentence summary"",
  ""keyPoints"": [""point 1"", ""point 2"", ""point 3""],
  ""recommendedActions"": [""action 1"", ""action 2""],
  ""riskLevel"": ""Low or Medium or High""
}}

Title: {title}
Content: {content}

Risk levels:
- High: Critical issues, security vulnerabilities, outages
- Medium: Important issues, performance problems
- Low: Routine information, minor issues

Return ONLY the JSON object.";

            var requestBody = new GeminiRequest
            {
                Contents = new List<GeminiContent>
                {
                    new GeminiContent
                    {
                        Parts = new List<GeminiPart>
                        {
                            new GeminiPart { Text = prompt }
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(
                requestBody,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            var url =
                $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey}";

            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            _logger.LogDebug("Sending request to Gemini API");

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Gemini API error: {StatusCode} - {Error}",
                    response.StatusCode,
                    errorContent);

                throw new HttpRequestException($"Gemini API returned {response.StatusCode}");
            }

            var responseContent =
                await response.Content.ReadAsStringAsync(cancellationToken);

            using var doc = JsonDocument.Parse(responseContent);

            var textContent = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            if (string.IsNullOrWhiteSpace(textContent))
                throw new InvalidOperationException("Gemini API returned empty text content");

            // ✅ Robust cleanup & extraction
            textContent = ExtractJsonObject(textContent);

            _logger.LogInformation("Gemini cleaned JSON: {Json}", textContent);

            var enrichment = JsonSerializer.Deserialize<AiEnrichmentResponse>(
                textContent,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (enrichment == null)
                throw new InvalidOperationException("Failed to parse Gemini response");

            _logger.LogInformation("Successfully enriched note with Gemini AI");
            return enrichment;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Gemini API");
            throw;
        }
    }

    // ✅ Handles ```json fences and extra text
    private static string ExtractJsonObject(string text)
    {
        text = text
            .Replace("```json", "", StringComparison.OrdinalIgnoreCase)
            .Replace("```", "", StringComparison.OrdinalIgnoreCase)
            .Trim();

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');

        if (start >= 0 && end > start)
            return text.Substring(start, end - start + 1).Trim();

        return text;
    }

    private static AiEnrichmentResponse GetMockResponse(string title, string content)
    {
        var contentLength = content.Length;
        var riskLevel = contentLength > 500 ? "High"
                        : contentLength > 200 ? "Medium"
                        : "Low";

        return new AiEnrichmentResponse
        {
            Summary = $"Mock summary for: {title}",
            KeyPoints = new List<string>
            {
                "Mock key point 1",
                "Mock key point 2",
                "Mock key point 3"
            },
            RecommendedActions = new List<string>
            {
                "Mock action 1",
                "Mock action 2"
            },
            RiskLevel = riskLevel
        };
    }

    // ===== Gemini request DTOs =====
    private class GeminiRequest
    {
        public List<GeminiContent> Contents { get; set; } = new();
    }

    private class GeminiContent
    {
        public List<GeminiPart> Parts { get; set; } = new();
    }

    private class GeminiPart
    {
        public string Text { get; set; } = string.Empty;
    }
}
