using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.Configurations;

public class PlayItemEntityTypeConfiguration : IEntityTypeConfiguration<PlayItem>
{
    public void Configure(EntityTypeBuilder<PlayItem> builder)
    {
        builder
            .Property(pd => pd.Id)
            .ValueGeneratedNever();

        builder
            .HasIndex(pd => pd.PlayId)
            .IsClustered(false);

        builder
            .HasIndex(pd => pd.UserId)
            .IsClustered(false);

        builder
            .HasIndex(pd => pd.OrganizationId)
            .IsClustered(false);

        builder
            .HasIndex(pd => pd.ProviderId)
            .IsClustered(false);

        builder
            .HasOne(pd => pd.User)
            .WithMany()
            .HasForeignKey(pd => pd.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(pd => pd.Organization)
            .WithMany()
            .HasForeignKey(pd => pd.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(pd => pd.Provider)
            .WithMany()
            .HasForeignKey(pd => pd.ProviderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .ToTable(nameof(PlayItem))
            .HasCheckConstraint(
                "CK_PlayItem_UserOrOrganizationOrProvider",
                "((CASE WHEN \"UserId\" IS NOT NULL THEN 1 ELSE 0 END) + (CASE WHEN \"OrganizationId\" IS NOT NULL THEN 1 ELSE 0 END) + (CASE WHEN \"ProviderId\" IS NOT NULL THEN 1 ELSE 0 END)) = 1"
            );
    }
}
