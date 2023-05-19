using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.Configurations;

public class TaxRateEntityTypeConfiguration : IEntityTypeConfiguration<TaxRate>
{
    public void Configure(EntityTypeBuilder<TaxRate> builder)
    {
        builder
            .HasIndex(tr => new { tr.Country, tr.PostalCode })
            .HasFilter($"{nameof(TaxRate.Active)} = 1")
            .IsUnique();

        builder.ToTable(nameof(TaxRate));
    }
}
