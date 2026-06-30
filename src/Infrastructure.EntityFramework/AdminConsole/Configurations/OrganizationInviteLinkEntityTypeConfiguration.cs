using Bit.Infrastructure.EntityFramework.AdminConsole.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Configurations;

public class OrganizationInviteLinkEntityTypeConfiguration : IEntityTypeConfiguration<OrganizationInviteLink>
{
    public void Configure(EntityTypeBuilder<OrganizationInviteLink> builder)
    {
        builder
            .Property(e => e.Id)
            .ValueGeneratedNever();

        builder
            .HasIndex(e => e.OrganizationId)
            .IsUnique()
            .IsClustered(false);

        builder
            .HasIndex(e => e.Code)
            .IsUnique()
            .IsClustered(false);

        builder.ToTable(nameof(OrganizationInviteLink));
    }
}
