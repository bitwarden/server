using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.Configurations;

public class OrganizationUserEntityTypeConfiguration : IEntityTypeConfiguration<OrganizationUser>
{
    public void Configure(EntityTypeBuilder<OrganizationUser> builder)
    {
        builder.Property(ou => ou.Id).ValueGeneratedNever();

        builder.HasIndex(ou => ou.OrganizationId).IsClustered(false);

        builder.HasIndex(ou => ou.UserId).IsClustered(false);

        builder.ToTable(nameof(OrganizationUser));
    }
}
