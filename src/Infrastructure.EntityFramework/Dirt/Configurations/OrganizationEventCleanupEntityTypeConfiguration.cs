using Bit.Infrastructure.EntityFramework.Dirt.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.Dirt.Configurations;

public class OrganizationEventCleanupEntityTypeConfiguration : IEntityTypeConfiguration<OrganizationEventCleanup>
{
    public void Configure(EntityTypeBuilder<OrganizationEventCleanup> builder)
    {
        builder
            .Property(s => s.Id)
            .ValueGeneratedNever();

        builder.HasKey(s => s.Id)
            .IsClustered();

        builder
            .HasIndex(s => new { s.CompletedAt, s.QueuedAt })
            .IsClustered(false)
            .HasDatabaseName("IX_OrganizationEventCleanup_CompletedAt_QueuedAt");

        builder.ToTable(nameof(OrganizationEventCleanup));
    }
}
