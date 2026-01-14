using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KnowledgeService.Models.DTOs;

public class AiEnrichmentResponse
{
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("keyPoints")]
    public List<string> KeyPoints { get; set; } = new();

    [JsonPropertyName("recommendedActions")]
    public List<string> RecommendedActions { get; set; } = new();

    [JsonPropertyName("riskLevel")]
    public string RiskLevel { get; set; } = string.Empty;
}