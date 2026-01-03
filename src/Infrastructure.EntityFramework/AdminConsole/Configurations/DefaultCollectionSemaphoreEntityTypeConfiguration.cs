using Bit.Infrastructure.EntityFramework.AdminConsole.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Configurations;

public class DefaultCollectionSemaphoreEntityTypeConfiguration : IEntityTypeConfiguration<DefaultCollectionSemaphore>
{
    public void Configure(EntityTypeBuilder<DefaultCollectionSemaphore> builder)
    {
        builder
            .HasKey(dcs => new { dcs.OrganizationUserId });

        // OrganizationUser FK cascades deletions to ensure automatic cleanup
        builder
            .HasOne(dcs => dcs.OrganizationUser)
            .WithMany()
            .HasForeignKey(dcs => dcs.OrganizationUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(nameof(DefaultCollectionSemaphore));
    }
}
