using Microsoft.EntityFrameworkCore;
using LocalLeadGen.Domain.Entities;
using LocalLeadGen.Domain.ValueObjects;

namespace LocalLeadGen.Infrastructure.Persistence;

/// <summary>
/// DbContext di Entity Framework Core 9 per la persistenza SQLite della pipeline.
/// </summary>
public class AppDbContext : DbContext
{
    public DbSet<Lead> Leads => Set<Lead>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Lead>(entity =>
        {
            entity.ToTable("Leads");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity.Property(e => e.BusinessName)
                .IsRequired()
                .HasMaxLength(250);

            entity.Property(e => e.Category)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.City)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.Address)
                .IsRequired()
                .HasMaxLength(300);

            entity.Property(e => e.Phone)
                .HasMaxLength(50);

            entity.Property(e => e.WebsiteUrl)
                .HasMaxLength(500);

            // Mapping del Value Object EmailAddress come stringa
            entity.Property(e => e.ContactEmail)
                .HasConversion(
                    email => email != null ? email.Value : null,
                    value => string.IsNullOrWhiteSpace(value) ? null : EmailAddress.Create(value))
                .HasMaxLength(254);

            entity.Property(e => e.HasWebsite)
                .IsRequired();

            entity.Property(e => e.AuditNotes)
                .HasMaxLength(2000);

            entity.Property(e => e.PitchAngle)
                .HasMaxLength(500);

            entity.Property(e => e.DraftSubject)
                .HasMaxLength(300);

            entity.Property(e => e.Status)
                .IsRequired()
                .HasConversion<string>();

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired();

            entity.Property(e => e.FailureReason)
                .HasMaxLength(1000);

            // Indice composito per velocizzare la deduplicazione aziendale su base cittadina
            entity.HasIndex(e => new { e.BusinessName, e.City })
                .HasDatabaseName("IX_Leads_BusinessName_City");

            entity.HasIndex(e => e.CreatedAtUtc)
                .HasDatabaseName("IX_Leads_CreatedAtUtc");
        });
    }
}
