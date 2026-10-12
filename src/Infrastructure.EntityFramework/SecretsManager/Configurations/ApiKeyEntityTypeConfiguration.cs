using Bit.Infrastructure.EntityFramework.SecretsManager.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage;

namespace Bit.Infrastructure.EntityFramework.SecretsManager.Configurations;

/// <remarks>
/// Not picked up by the assembly scan (no parameterless constructor); <see cref="Repositories.DatabaseContext"/>
/// applies it with the provider's SQL helper so the check constraint quotes identifiers correctly on MySQL.
/// </remarks>
public class ApiKeyEntityTypeConfiguration(ISqlGenerationHelper sql) : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder
            .Property(s => s.Id)
            .ValueGeneratedNever();

        builder
            .HasKey(s => s.Id)
            .IsClustered();

        builder
            .HasIndex(s => s.ServiceAccountId)
            .IsClustered(false);

        builder
            .HasIndex(s => s.OrganizationId)
            .IsClustered(false);

        builder.ToTable(nameof(ApiKey), t => t.HasCheckConstraint(
            "CK_ApiKey_SingleOwner",
            $"{sql.DelimitIdentifier(nameof(ApiKey.ServiceAccountId))} IS NULL OR " +
            $"{sql.DelimitIdentifier(nameof(ApiKey.OrganizationId))} IS NULL"));
    }
}
