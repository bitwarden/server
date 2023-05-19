using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.Configurations;

public class DeviceEntityTypeConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder
            .HasIndex(d => new { d.UserId, d.Identifier })
            .IsUnique()
            .IsClustered(false);

        builder
            .HasIndex(d => d.Identifier)
            .IsClustered(false);

        builder.ToTable(nameof(Device));
    }
}
