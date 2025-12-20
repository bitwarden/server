using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Configurations;

public class CollectionEntityTypeConfiguration : IEntityTypeConfiguration<Collection>
{
    public void Configure(EntityTypeBuilder<Collection> builder)
    {
        builder
            .Property(c => c.Id)
            .ValueGeneratedNever();

        builder
            .HasIndex(c => new { c.DefaultCollectionOwner, c.OrganizationId, c.Type })
            .IsUnique()
            .HasFilter("[Type] = 1")
            .HasDatabaseName("IX_Collection_DefaultCollectionOwner_OrganizationId_Type");

        builder.ToTable(nameof(Collection));
    }
}