using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace KnowledgeService.Models.DTOs;

public class UpdateNoteDto
{
    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;

    public List<string>? Tags { get; set; }
}