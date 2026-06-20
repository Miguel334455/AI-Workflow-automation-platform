using Microsoft.EntityFrameworkCore;
using TriggerService.API.Domain;

namespace TriggerService.API.Infrastructure;

public class TriggerDbContext : DbContext
{
    public TriggerDbContext(DbContextOptions<TriggerDbContext> options) : base(options) { }

    public DbSet<Trigger> Triggers => Set<Trigger>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Trigger>(b =>
        {
            b.HasKey(t => t.Id);
            b.Property(t => t.ConfigJson).HasColumnType("nvarchar(max)");
            b.Property(t => t.Type).HasConversion<string>().HasMaxLength(20);
            b.HasIndex(t => t.WorkflowId);
        });
    }
}
