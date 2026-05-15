using Bit.Infrastructure.EntityFramework.Billing.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.Billing.Configurations;

public class OrganizationPlanMigrationCohortEntityTypeConfiguration
    : IEntityTypeConfiguration<OrganizationPlanMigrationCohort>
{
    public void Configure(EntityTypeBuilder<OrganizationPlanMigrationCohort> builder)
    {
        builder
            .Property(c => c.Id)
            .ValueGeneratedNever();

        builder
            .HasKey(c => c.Id)
            .IsClustered();

        builder
            .HasIndex(c => c.Name)
            .IsUnique()
            .IsClustered(false);

        builder.ToTable(nameof(OrganizationPlanMigrationCohort));
    }
}
