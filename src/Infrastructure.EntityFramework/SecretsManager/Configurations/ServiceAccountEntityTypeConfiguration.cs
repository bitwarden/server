using Bit.Infrastructure.EntityFramework.SecretsManager.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.SecretsManager.Configurations;

public class ServiceAccountEntityTypeConfiguration : IEntityTypeConfiguration<ServiceAccount>
{
    public void Configure(EntityTypeBuilder<ServiceAccount> builder)
    {
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.HasKey(s => s.Id).IsClustered();

        builder.HasIndex(s => s.OrganizationId).IsClustered(false);

        builder.ToTable(nameof(ServiceAccount));
    }
}
