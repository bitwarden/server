using Bit.Infrastructure.EntityFramework.AdminConsole.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Configurations;

public class OrganizationIntegrationConfigurationEntityTypeConfiguration : IEntityTypeConfiguration<OrganizationIntegrationConfiguration>
{
    public void Configure(EntityTypeBuilder<OrganizationIntegrationConfiguration> builder)
    {
        builder
            .Property(p => p.Id)
            .ValueGeneratedNever();

        builder.ToTable(nameof(OrganizationIntegrationConfiguration));
    }
}
