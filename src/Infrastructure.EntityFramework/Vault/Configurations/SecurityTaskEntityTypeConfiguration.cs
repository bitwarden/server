using Bit.Infrastructure.EntityFramework.Vault.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.Vault.Configurations;

public class SecurityTaskEntityTypeConfiguration : IEntityTypeConfiguration<SecurityTask>
{
    public void Configure(EntityTypeBuilder<SecurityTask> builder)
    {
        builder
            .Property(s => s.Id)
            .ValueGeneratedNever();

        builder
            .HasKey(s => s.Id)
            .IsClustered();

        builder
            .HasIndex(s => s.OrganizationId)
            .IsClustered(false);

        builder
            .HasIndex(s => s.CipherId)
            .IsClustered(false);

        builder
            .HasOne(p => p.Organization)
            .WithMany()
            .HasForeignKey(p => p.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(p => p.Cipher)
            .WithMany()
            .HasForeignKey(p => p.CipherId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .ToTable(nameof(SecurityTask));
    }
}
