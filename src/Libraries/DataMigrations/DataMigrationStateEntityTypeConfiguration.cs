using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.DataMigrations;

internal class DataMigrationStateEntityTypeConfiguration : IEntityTypeConfiguration<DataMigrationState>
{
    public void Configure(EntityTypeBuilder<DataMigrationState> builder)
    {
        builder.ToTable(nameof(DataMigrationState));
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Name).HasMaxLength(100);
        builder.Property(s => s.RangeStart).HasMaxLength(300);
        builder.Property(s => s.RangeEnd).HasMaxLength(300);
        builder.Property(s => s.Cursor).HasMaxLength(300);
        builder.Property(s => s.LeaseOwner).HasMaxLength(100);
        builder.HasIndex(s => new { s.Name, s.Partition }).IsUnique();
    }
}
