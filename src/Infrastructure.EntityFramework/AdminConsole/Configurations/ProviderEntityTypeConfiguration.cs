using Bit.Infrastructure.EntityFramework.AdminConsole.Models.Provider;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Configurations;

public class ProviderEntityTypeConfiguration : IEntityTypeConfiguration<Provider>
{
    public void Configure(EntityTypeBuilder<Provider> builder)
    {
        builder
            .Property(p => p.Id)
            .ValueGeneratedNever();

        builder.HasIndex(p => p.GatewayCustomerId);
        builder.HasIndex(p => p.GatewaySubscriptionId);

        builder.ToTable(nameof(Provider));
    }
}
