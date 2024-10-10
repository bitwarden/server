using Bit.Infrastructure.EntityFramework.Billing.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.Billing.Configurations;

public class ClientOrganizationMigrationRecordEntityTypeConfiguration : IEntityTypeConfiguration<ClientOrganizationMigrationRecord>
{
    public void Configure(EntityTypeBuilder<ClientOrganizationMigrationRecord> builder)
    {
        builder
            .Property(c => c.Id)
            .ValueGeneratedNever();

        builder
            .HasIndex(migrationRecord => new { migrationRecord.ProviderId, migrationRecord.OrganizationId })
            .IsUnique();

        builder.ToTable(nameof(ClientOrganizationMigrationRecord));
    }
}
