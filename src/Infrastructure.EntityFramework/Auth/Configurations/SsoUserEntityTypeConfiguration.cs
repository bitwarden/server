using Bit.Infrastructure.EntityFramework.Auth.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.Auth.Configurations;

public class SsoUserEntityTypeConfiguration : IEntityTypeConfiguration<SsoUser>
{
    public void Configure(EntityTypeBuilder<SsoUser> builder)
    {
        builder
            .HasIndex(su => su.OrganizationId)
            .IsClustered(false);

        NpgsqlIndexBuilderExtensions.IncludeProperties(
            builder.HasIndex(su => new { su.OrganizationId, su.ExternalId })
                .IsUnique()
                .IsClustered(false),
            su => su.UserId);

        builder
            .HasIndex(su => new { su.OrganizationId, su.UserId })
            .IsUnique()
            .IsClustered(false);

        builder.ToTable(nameof(SsoUser));
    }
}
