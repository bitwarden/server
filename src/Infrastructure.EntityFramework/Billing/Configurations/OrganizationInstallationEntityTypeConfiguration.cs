using Bit.Infrastructure.EntityFramework.Billing.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.Billing.Configurations;

public class OrganizationInstallationEntityTypeConfiguration : IEntityTypeConfiguration<OrganizationInstallation>
{
    public void Configure(EntityTypeBuilder<OrganizationInstallation> builder)
    {
        builder
            .Property(oi => oi.Id)
            .ValueGeneratedNever();

        builder
            .HasKey(oi => oi.Id)
            .IsClustered();

        builder
            .HasIndex(oi => oi.OrganizationId)
            .IsClustered(false);

        builder
            .HasIndex(oi => oi.InstallationId)
            .IsClustered(false);

        builder.ToTable(nameof(OrganizationInstallation));
    }
}
