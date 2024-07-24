using Bit.Core.Billing.Entities;
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

        builder.ToTable(nameof(ClientOrganizationMigrationRecord));
    }
}
