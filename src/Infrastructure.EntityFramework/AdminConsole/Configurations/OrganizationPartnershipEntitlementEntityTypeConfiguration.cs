using Bit.Infrastructure.EntityFramework.AdminConsole.Models;
using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Configurations;

public class OrganizationPartnershipEntitlementEntityTypeConfiguration
    : IEntityTypeConfiguration<OrganizationPartnershipEntitlement>
{
    public void Configure(EntityTypeBuilder<OrganizationPartnershipEntitlement> builder)
    {
        builder
            .Property(e => e.Id)
            .ValueGeneratedNever();

        // The column stores the protected value, so the entity's plaintext length limit does not apply.
        builder
            .Property(e => e.ExternalId)
            .Metadata.SetMaxLength(null);

        builder
            .HasOne<OrganizationPartnership>()
            .WithMany()
            .HasForeignKey(e => e.OrganizationPartnershipId)
            .OnDelete(DeleteBehavior.NoAction);

        builder
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder
            .HasIndex(e => new { e.OrganizationPartnershipId, e.ExternalIdHash })
            .IsUnique()
            .IsClustered(false);

        builder
            .HasIndex(e => new { e.State, e.ResumeWindowExpirationDate })
            .IsClustered(false);

        builder.ToTable(nameof(OrganizationPartnershipEntitlement));
    }
}
