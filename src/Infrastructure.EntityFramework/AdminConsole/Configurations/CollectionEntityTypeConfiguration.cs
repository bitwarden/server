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
            .HasIndex(c => new { c.DefaultCollectionOwnerId, c.OrganizationId, c.Type })
            .IsUnique()
            .HasFilter("[Type] = 1")
            .HasDatabaseName("IX_Collection_DefaultCollectionOwnerId_OrganizationId_Type");

        // Configure FK with NO ACTION delete behavior to prevent cascade conflicts
        // Cleanup is handled explicitly in OrganizationUserRepository.DeleteAsync
        builder
            .HasOne<OrganizationUser>()
            .WithMany()
            .HasForeignKey(c => c.DefaultCollectionOwnerId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.ToTable(nameof(Collection));
    }
}
