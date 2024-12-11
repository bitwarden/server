using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.Configurations;

public class CacheEntityTypeConfiguration : IEntityTypeConfiguration<Cache>
{
    public void Configure(EntityTypeBuilder<Cache> builder)
    {
        builder.HasKey(s => s.Id).IsClustered();

        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.HasIndex(s => s.ExpiresAtTime).IsClustered(false);

        builder.ToTable(nameof(Cache));
    }
}
