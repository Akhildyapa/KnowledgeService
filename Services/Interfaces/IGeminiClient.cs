using KnowledgeService.Models.DTOs;
using System.Threading.Tasks;
using System.Threading;

namespace KnowledgeService.Services.Interfaces;

public interface IGeminiClient
{
    Task<AiEnrichmentResponse> EnrichKnowledgeNoteAsync(string title, string content, CancellationToken cancellationToken = default);
}