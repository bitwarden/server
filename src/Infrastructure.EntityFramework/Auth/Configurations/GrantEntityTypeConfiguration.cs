using Bit.Infrastructure.EntityFramework.Auth.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.Auth.Configurations;

public class GrantEntityTypeConfiguration : IEntityTypeConfiguration<Grant>
{
    public void Configure(EntityTypeBuilder<Grant> builder)
    {
        builder
            .HasKey(s => s.Id)
            .IsClustered();

        builder
            .Property(s => s.Id)
            .UseIdentityColumn();

        builder
            .HasIndex(s => s.Key)
            .IsUnique(true);

        builder.ToTable(nameof(Grant));
    }
}
