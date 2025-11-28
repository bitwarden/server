using Bit.Infrastructure.EntityFramework.Billing.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.Billing.Configurations;

public class ProviderInvoiceItemEntityTypeConfiguration : IEntityTypeConfiguration<ProviderInvoiceItem>
{
    public void Configure(EntityTypeBuilder<ProviderInvoiceItem> builder)
    {
        builder
            .Property(t => t.Id)
            .ValueGeneratedNever();

        builder.ToTable(nameof(ProviderInvoiceItem));
    }
}
