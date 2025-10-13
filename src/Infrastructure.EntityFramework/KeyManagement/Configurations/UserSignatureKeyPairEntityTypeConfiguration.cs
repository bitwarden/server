using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.Configurations;

public class UserSignatureKeyPairEntityTypeConfiguration : IEntityTypeConfiguration<UserSignatureKeyPair>
{
    public void Configure(EntityTypeBuilder<UserSignatureKeyPair> builder)
    {
        builder
            .Property(s => s.Id)
            .ValueGeneratedNever();

        builder
            .HasIndex(s => s.UserId)
            .IsUnique()
            .IsClustered(false);

        builder.ToTable(nameof(UserSignatureKeyPair));
    }
}
