using Microsoft.EntityFrameworkCore;
using WorkflowService.API.Domain;

namespace WorkflowService.API.Infrastructure;

public class WorkflowDbContext : DbContext
{
    public WorkflowDbContext(DbContextOptions<WorkflowDbContext> options) : base(options) { }

    public DbSet<Workflow> Workflows => Set<Workflow>();
    public DbSet<WorkflowNode> WorkflowNodes => Set<WorkflowNode>();
    public DbSet<WorkflowConnection> WorkflowConnections => Set<WorkflowConnection>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Workflow>(b =>
        {
            b.HasKey(w => w.Id);
            b.Property(w => w.Name).IsRequired().HasMaxLength(200);
            b.HasMany(w => w.Nodes)
                .WithOne(n => n.Workflow)
                .HasForeignKey(n => n.WorkflowId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasMany(w => w.Connections)
                .WithOne(c => c.Workflow)
                .HasForeignKey(c => c.WorkflowId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkflowNode>(b =>
        {
            b.HasKey(n => n.Id);
            b.Property(n => n.Name).IsRequired().HasMaxLength(200);
            b.Property(n => n.Type).IsRequired().HasMaxLength(50);
            b.Property(n => n.ConfigJson).HasColumnType("nvarchar(max)");
        });

        modelBuilder.Entity<WorkflowConnection>(b =>
        {
            b.HasKey(c => c.Id);
        });
    }
}
