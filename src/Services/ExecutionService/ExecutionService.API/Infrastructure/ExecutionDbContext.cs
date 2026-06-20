using ExecutionService.API.Domain;
using Microsoft.EntityFrameworkCore;

namespace ExecutionService.API.Infrastructure;

public class ExecutionDbContext : DbContext
{
    public ExecutionDbContext(DbContextOptions<ExecutionDbContext> options) : base(options) { }

    public DbSet<WorkflowRun> WorkflowRuns => Set<WorkflowRun>();
    public DbSet<NodeExecution> NodeExecutions => Set<NodeExecution>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkflowRun>(b =>
        {
            b.HasKey(r => r.Id);
            b.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(r => r.InputPayloadJson).HasColumnType("nvarchar(max)");
            b.HasMany(r => r.NodeExecutions)
                .WithOne(n => n.Run)
                .HasForeignKey(n => n.RunId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(r => r.WorkflowId);
        });

        modelBuilder.Entity<NodeExecution>(b =>
        {
            b.HasKey(n => n.Id);
            b.Property(n => n.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(n => n.InputJson).HasColumnType("nvarchar(max)");
            b.Property(n => n.OutputJson).HasColumnType("nvarchar(max)");
            b.HasIndex(n => n.RunId);
        });
    }
}
