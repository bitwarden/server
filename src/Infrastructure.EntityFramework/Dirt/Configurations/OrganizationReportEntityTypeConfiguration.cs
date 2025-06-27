using Bit.Infrastructure.EntityFramework.Dirt.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.Dirt.Configurations;

public class OrganizationReportEntityTypeConfiguration : IEntityTypeConfiguration<OrganizationReport>
{
    public void Configure(EntityTypeBuilder<OrganizationReport> builder)
    {
        builder
            .Property(s => s.Id)
            .ValueGeneratedNever();

        builder.HasIndex(s => s.Id)
            .IsClustered(true);

        builder
            .HasIndex(s => s.OrganizationId)
            .IsClustered(false);

        builder
            .HasOne(s => s.Organization)
            .WithMany()
            .HasForeignKey(s => s.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(nameof(OrganizationReport));
    }
}
