using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.Configurations;

public class OrganizationEntityTypeConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder
            .Property(o => o.Id)
            .ValueGeneratedNever();

        NpgsqlIndexBuilderExtensions.IncludeProperties(
            builder.HasIndex(o => new { o.Id, o.Enabled }),
            o => o.UseTotp);

        builder
            .HasIndex(o => o.Identifier)
            .HasFilter($"{nameof(Organization.Identifier)} IS NOT NULL")
            .IsUnique();

        builder.ToTable(nameof(Organization));
    }
}
