using Bit.Infrastructure.EntityFramework.Billing.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.Billing.Configurations;

public class ProviderPlanEntityTypeConfiguration : IEntityTypeConfiguration<ProviderPlan>
{
    public void Configure(EntityTypeBuilder<ProviderPlan> builder)
    {
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.HasIndex(providerPlan => new { providerPlan.Id, providerPlan.PlanType }).IsUnique();

        builder.ToTable(nameof(ProviderPlan));
    }
}
