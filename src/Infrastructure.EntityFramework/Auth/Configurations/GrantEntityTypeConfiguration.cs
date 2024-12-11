using Bit.Infrastructure.EntityFramework.Auth.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.Auth.Configurations;

public class GrantEntityTypeConfiguration : IEntityTypeConfiguration<Grant>
{
    public void Configure(EntityTypeBuilder<Grant> builder)
    {
        builder.HasKey(s => s.Id).HasName("PK_Grant").IsClustered();

        builder.HasIndex(s => s.Key).IsUnique(true);

        builder.HasIndex(s => s.ExpirationDate).IsClustered(false);

        builder.ToTable(nameof(Grant));
    }
}
