using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.Configurations;

public class PlayDataEntityTypeConfiguration : IEntityTypeConfiguration<PlayData>
{
    public void Configure(EntityTypeBuilder<PlayData> builder)
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
            .ToTable(nameof(PlayData))
            .HasCheckConstraint(
                "CK_PlayData_UserOrOrganization",
                "(\"UserId\" IS NOT NULL AND \"OrganizationId\" IS NULL) OR (\"UserId\" IS NULL AND \"OrganizationId\" IS NOT NULL)"
            );
    }
}
