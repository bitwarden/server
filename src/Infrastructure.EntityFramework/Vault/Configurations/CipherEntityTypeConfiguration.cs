using Bit.Infrastructure.EntityFramework.Vault.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.Vault.Configurations;

public class CipherEntityTypeConfiguration : IEntityTypeConfiguration<Cipher>
{
    public void Configure(EntityTypeBuilder<Cipher> builder)
    {
        builder
            .Property(c => c.Id)
            .ValueGeneratedNever();

        NpgsqlIndexBuilderExtensions.IncludeProperties(
            builder.HasIndex(c => new { c.UserId, c.OrganizationId })
                .IsClustered(false),
            c =>
                new
                {
                    c.Type,
                    c.Data,
                    c.Favorites,
                    c.Folders,
                    c.Attachments,
                    c.CreationDate,
                    c.RevisionDate,
                    c.DeletedDate,
                });

        builder
            .HasIndex(c => c.OrganizationId)
            .IsClustered(false);

        builder
            .HasIndex(c => c.DeletedDate)
            .IsClustered(false);

        builder.ToTable(nameof(Cipher));
    }
}
