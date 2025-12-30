using Bit.Infrastructure.EntityFramework.AdminConsole.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Configurations;

public class DefaultCollectionSemaphoreEntityTypeConfiguration : IEntityTypeConfiguration<DefaultCollectionSemaphore>
{
    public void Configure(EntityTypeBuilder<DefaultCollectionSemaphore> builder)
    {
        builder
            .HasKey(dcs => new { dcs.OrganizationId, dcs.OrganizationUserId });

        // Cascade behavior: Organization -> OrganizationUser (CASCADE) -> DefaultCollectionSemaphore (CASCADE)
        // Organization FK uses NoAction to avoid competing cascade paths
        builder
            .HasOne(dcs => dcs.Organization)
            .WithMany()
            .HasForeignKey(dcs => dcs.OrganizationId)
            .OnDelete(DeleteBehavior.NoAction);

        // OrganizationUser FK cascades deletions to ensure automatic cleanup
        builder
            .HasOne(dcs => dcs.OrganizationUser)
            .WithMany()
            .HasForeignKey(dcs => dcs.OrganizationUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(nameof(DefaultCollectionSemaphore));
    }
}
