using Bit.Infrastructure.EntityFramework.AdminConsole.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Configurations;

public class OrganizationPartnershipReconciliationExportEntityTypeConfiguration
    : IEntityTypeConfiguration<OrganizationPartnershipReconciliationExport>
{
    public void Configure(EntityTypeBuilder<OrganizationPartnershipReconciliationExport> builder)
    {
        builder
            .Property(e => e.Id)
            .ValueGeneratedNever();

        builder
            .HasOne<OrganizationPartnership>()
            .WithMany()
            .HasForeignKey(e => e.OrganizationPartnershipId)
            .OnDelete(DeleteBehavior.NoAction);

        builder
            .HasIndex(e => e.OrganizationPartnershipId)
            .IsClustered(false);

        builder
            .HasIndex(e => e.Status)
            .IsClustered(false);

        builder
            .HasIndex(e => e.ExpirationDate)
            .IsClustered(false);

        builder.ToTable(nameof(OrganizationPartnershipReconciliationExport));
    }
}
