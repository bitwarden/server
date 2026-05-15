using Bit.Infrastructure.EntityFramework.Billing.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.Billing.Configurations;

public class OrganizationPlanMigrationCohortAssignmentEntityTypeConfiguration
    : IEntityTypeConfiguration<OrganizationPlanMigrationCohortAssignment>
{
    public void Configure(EntityTypeBuilder<OrganizationPlanMigrationCohortAssignment> builder)
    {
        builder
            .Property(a => a.Id)
            .ValueGeneratedNever();

        builder
            .HasKey(a => a.Id)
            .IsClustered();

        builder
            .HasIndex(a => a.OrganizationId)
            .IsUnique()
            .IsClustered(false);

        // Composite index supports the Pending/Scheduled/Migrated counts query consumed by
        // the Cohort Management aggregate (PM-36951).
        builder
            .HasIndex(a => new { a.CohortId, a.ScheduledAt, a.MigratedAt })
            .IsClustered(false);

        builder
            .HasOne(a => a.Organization)
            .WithMany()
            .HasForeignKey(a => a.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(a => a.Cohort)
            .WithMany()
            .HasForeignKey(a => a.CohortId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(nameof(OrganizationPlanMigrationCohortAssignment));
    }
}
