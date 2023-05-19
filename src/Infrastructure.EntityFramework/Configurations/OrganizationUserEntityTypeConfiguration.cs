using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.Configurations;

public class OrganizationUserEntityTypeConfiguration : IEntityTypeConfiguration<OrganizationUser>
{
    public void Configure(EntityTypeBuilder<OrganizationUser> builder)
    {
        builder
            .Property(ou => ou.Id)
            .ValueGeneratedNever();

        NpgsqlIndexBuilderExtensions.IncludeProperties(
            builder.HasIndex(ou => new { ou.UserId, ou.OrganizationId, ou.Status }).IsClustered(false),
            ou => ou.AccessAll);

        builder
            .HasIndex(ou => ou.OrganizationId)
            .IsClustered(false);

        builder.ToTable(nameof(OrganizationUser));
    }
}
