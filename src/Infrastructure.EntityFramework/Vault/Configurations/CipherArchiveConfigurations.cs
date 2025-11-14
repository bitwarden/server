using Bit.Infrastructure.EntityFramework.Vault.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.Vault.Configurations;

public class CipherArchiveConfiguration : IEntityTypeConfiguration<CipherArchive>
{
    public void Configure(EntityTypeBuilder<CipherArchive> builder)
    {
        builder.ToTable(nameof(CipherArchive));

        builder.HasKey(ca => new { ca.CipherId, ca.UserId });

        builder
            .HasOne(ca => ca.Cipher)
            .WithMany()
            .HasForeignKey(ca => ca.CipherId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(ca => ca.User)
            .WithMany()
            .HasForeignKey(ca => ca.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(ca => ca.ArchivedDate).IsRequired();
    }
}
