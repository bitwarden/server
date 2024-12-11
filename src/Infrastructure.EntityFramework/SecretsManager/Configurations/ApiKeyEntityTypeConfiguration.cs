using Bit.Infrastructure.EntityFramework.SecretsManager.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.SecretsManager.Configurations;

public class ApiKeyEntityTypeConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.HasKey(s => s.Id).IsClustered();

        builder.HasIndex(s => s.ServiceAccountId).IsClustered(false);

        builder.ToTable(nameof(ApiKey));
    }
}
