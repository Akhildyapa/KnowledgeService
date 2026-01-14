using Microsoft.EntityFrameworkCore;
using KnowledgeService.Models.Entities;

namespace KnowledgeService.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<KnowledgeNote> KnowledgeNotes { get; set; }
}