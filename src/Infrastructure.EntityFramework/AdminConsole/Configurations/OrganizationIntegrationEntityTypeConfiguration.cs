using Bit.Infrastructure.EntityFramework.Dirt.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Configurations;

public class OrganizationIntegrationEntityTypeConfiguration : IEntityTypeConfiguration<OrganizationIntegration>
{
    public void Configure(EntityTypeBuilder<OrganizationIntegration> builder)
    {
        builder
            .Property(oi => oi.Id)
            .ValueGeneratedNever();

        builder
            .HasIndex(oi => oi.OrganizationId)
            .IsClustered(false);

        builder
            .HasIndex(oi => new { oi.OrganizationId, oi.Type })
            .IsUnique()
            .IsClustered(false);

        builder.ToTable(nameof(OrganizationIntegration));
    }
}
