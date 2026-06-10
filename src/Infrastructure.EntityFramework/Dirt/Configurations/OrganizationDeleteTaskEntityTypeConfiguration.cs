using Bit.Infrastructure.EntityFramework.Dirt.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.Dirt.Configurations;

public class OrganizationDeleteTaskEntityTypeConfiguration : IEntityTypeConfiguration<OrganizationDeleteTask>
{
    public void Configure(EntityTypeBuilder<OrganizationDeleteTask> builder)
    {
        builder
            .Property(t => t.Id)
            .ValueGeneratedNever();

        builder
            .HasIndex(t => t.Id)
            .IsClustered(true);

        // Mirrors IX_OrganizationDeleteTask_CompletedDate_CreationDate; drives the
        // pending-task lease query (claim oldest incomplete task first).
        builder
            .HasIndex(t => new { t.CompletedDate, t.CreationDate })
            .IsClustered(false);

        builder.ToTable(nameof(OrganizationDeleteTask));
    }
}
