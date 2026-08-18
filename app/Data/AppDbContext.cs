using Microsoft.EntityFrameworkCore;
using app.Models;

namespace app.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<SensorLogs> SensorLogs => Set<SensorLogs>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SensorLogs>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MoistureValue).IsRequired();
            entity.Property(e => e.TimestampUtc).IsRequired();
        });
    }
}