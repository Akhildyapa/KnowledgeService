using System;
using System.ComponentModel.DataAnnotations;

namespace KnowledgeService.Models.Entities;

public class KnowledgeNote
{
	[Key]
	public Guid Id { get; set; }

	[Required]
	[MaxLength(500)]
	public string Title { get; set; } = string.Empty;

	[Required]
	public string Content { get; set; } = string.Empty;

	[MaxLength(1000)]
	public string? Tags { get; set; }

	[Required]
	[MaxLength(256)]
	public string CreatedBy { get; set; } = string.Empty;

	public DateTime CreatedAtUtc { get; set; }

	// AI Fields
	public string? AiSummary { get; set; }
	public string? AiKeyPoints { get; set; }
	public string? AiActions { get; set; }

	[MaxLength(20)]
	public string? AiRiskLevel { get; set; }

	public DateTime? AiUpdatedAtUtc { get; set; }
}