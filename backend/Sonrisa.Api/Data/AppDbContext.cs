using Microsoft.EntityFrameworkCore;
using Sonrisa.Api.Data.Entities;

namespace Sonrisa.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<EarthquakeEvent> EarthquakeEvents => Set<EarthquakeEvent>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<Delivery> Deliveries => Set<Delivery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EarthquakeEvent>(entity =>
        {
            entity.HasIndex(item => item.SourceEventId).IsUnique();
            entity.Property(item => item.SourceEventId).IsRequired();
            entity.Property(item => item.Title).IsRequired();
            entity.Property(item => item.Place).IsRequired();
            entity.Property(item => item.SourceUrl).IsRequired();
        });

        modelBuilder.Entity<Alert>(entity =>
        {
            entity.Property(item => item.Name).IsRequired();
        });

        modelBuilder.Entity<Delivery>(entity =>
        {
            entity.HasIndex(item => new { item.EarthquakeEventId, item.AlertId, item.Channel })
                .IsUnique();
            entity.Property(item => item.Channel).HasConversion<string>();
            entity.Property(item => item.Status).HasConversion<string>();
            entity.HasOne<EarthquakeEvent>()
                .WithMany()
                .HasForeignKey(item => item.EarthquakeEventId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Alert>()
                .WithMany()
                .HasForeignKey(item => item.AlertId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
