using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KnowledgeService.Data;
using KnowledgeService.Models.DTOs;
using KnowledgeService.Models.Entities;
using System.Text.Json;
using System.Security.Claims;
using KnowledgeService.Services.Interfaces;

namespace KnowledgeService.Controllers;

[ApiController]
[Route("api/notes")]
[Authorize]
public class NotesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<NotesController> _logger;
    private readonly IGeminiClient _geminiClient;

    public NotesController(ApplicationDbContext context, ILogger<NotesController> logger, IGeminiClient geminiClient)
    {
        _context = context;
        _logger = logger;
        _geminiClient = geminiClient;
    }

    [HttpPost]
    public async Task<ActionResult<NoteResponseDto>> CreateNote([FromBody] CreateNoteDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";

        var note = new KnowledgeNote
        {
            Id = Guid.NewGuid(),
            Title = dto.Title,
            Content = dto.Content,
            Tags = dto.Tags != null ? JsonSerializer.Serialize(dto.Tags) : null,
            CreatedBy = userId,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.KnowledgeNotes.Add(note);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created note {NoteId} by user {UserId}", note.Id, userId);

        var response = MapToResponseDto(note);
        return CreatedAtAction(nameof(GetNoteById), new { id = note.Id }, response);
    }

    [HttpGet]
    public async Task<ActionResult<List<NoteResponseDto>>> GetNotes()
    {
        var notes = await _context.KnowledgeNotes
            .OrderByDescending(n => n.CreatedAtUtc)
            .ToListAsync();

        var response = notes.Select(MapToResponseDto).ToList();
        return Ok(response);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<NoteResponseDto>> GetNoteById(Guid id)
    {
        var note = await _context.KnowledgeNotes.FindAsync(id);

        if (note == null)
        {
            return NotFound(new { message = $"Note with ID {id} not found" });
        }

        return Ok(MapToResponseDto(note));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<NoteResponseDto>> UpdateNote(Guid id, [FromBody] UpdateNoteDto dto)
    {
        var note = await _context.KnowledgeNotes.FindAsync(id);

        if (note == null)
        {
            return NotFound(new { message = $"Note with ID {id} not found" });
        }

        note.Title = dto.Title;
        note.Content = dto.Content;
        note.Tags = dto.Tags != null ? JsonSerializer.Serialize(dto.Tags) : null;

        await _context.SaveChangesAsync();

        return Ok(MapToResponseDto(note));
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> DeleteNote(Guid id)
    {
        var note = await _context.KnowledgeNotes.FindAsync(id);

        if (note == null)
        {
            return NotFound(new { message = $"Note with ID {id} not found" });
        }

        _context.KnowledgeNotes.Remove(note);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("{id}/ai")]
    public async Task<ActionResult<AiEnrichmentResponse>> EnrichNote(Guid id)
    {
        try
        {
            var note = await _context.KnowledgeNotes.FindAsync(id);

            if (note == null)
            {
                return NotFound(new { message = $"Note with ID {id} not found" });
            }

            var enrichment = await _geminiClient.EnrichKnowledgeNoteAsync(note.Title, note.Content);

            note.AiSummary = enrichment.Summary;
            note.AiKeyPoints = JsonSerializer.Serialize(enrichment.KeyPoints);
            note.AiActions = JsonSerializer.Serialize(enrichment.RecommendedActions);
            note.AiRiskLevel = enrichment.RiskLevel;
            note.AiUpdatedAtUtc = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Enriched note {NoteId} with AI", id);

            return Ok(enrichment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enriching note {NoteId}", id);
            return StatusCode(500, new { message = "Error processing AI enrichment" });
        }
    }

    private static NoteResponseDto MapToResponseDto(KnowledgeNote note)
    {
        List<string>? tags = null;
        if (!string.IsNullOrWhiteSpace(note.Tags))
        {
            try
            {
                tags = JsonSerializer.Deserialize<List<string>>(note.Tags);
            }
            catch { }
        }

        return new NoteResponseDto
        {
            Id = note.Id,
            Title = note.Title,
            Content = note.Content,
            Tags = tags,
            CreatedBy = note.CreatedBy,
            CreatedAtUtc = note.CreatedAtUtc
        };
    }
}