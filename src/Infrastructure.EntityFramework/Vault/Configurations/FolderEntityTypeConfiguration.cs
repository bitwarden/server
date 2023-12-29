using Bit.Infrastructure.EntityFramework.Vault.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.Vault.Configurations;

public class FolderEntityTypeConfiguration : IEntityTypeConfiguration<Folder>
{
    public void Configure(EntityTypeBuilder<Folder> builder)
    {
        builder
            .Property(f => f.Id)
            .ValueGeneratedNever();

        NpgsqlIndexBuilderExtensions.IncludeProperties(
            builder.HasIndex(f => f.UserId)
                .IsClustered(false),
            f =>
                new { f.Name, f.CreationDate, f.RevisionDate });

        builder.ToTable(nameof(Folder));
    }
}
