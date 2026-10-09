using Bit.Infrastructure.EntityFramework.AdminConsole.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Configurations;

public class OrganizationScopedApiKeyEntityTypeConfiguration : IEntityTypeConfiguration<OrganizationScopedApiKey>
{
    public void Configure(EntityTypeBuilder<OrganizationScopedApiKey> builder)
    {
        builder
            .Property(e => e.Id)
            .ValueGeneratedNever();

        builder
            .HasIndex(e => e.OrganizationId)
            .IsClustered(false);

        builder.ToTable(nameof(OrganizationScopedApiKey));
    }
}
