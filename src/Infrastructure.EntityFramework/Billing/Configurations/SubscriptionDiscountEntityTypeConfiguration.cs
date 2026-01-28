using Bit.Infrastructure.EntityFramework.Billing.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.Billing.Configurations;

public class SubscriptionDiscountEntityTypeConfiguration : IEntityTypeConfiguration<SubscriptionDiscount>
{
    public void Configure(EntityTypeBuilder<SubscriptionDiscount> builder)
    {
        builder
            .Property(t => t.Id)
            .ValueGeneratedNever();

        builder
            .HasIndex(sd => sd.StripeCouponId)
            .IsUnique();

        var dateRangeIndex = builder
            .HasIndex(sd => new { sd.StartDate, sd.EndDate })
            .IsClustered(false)
            .HasDatabaseName("IX_SubscriptionDiscount_DateRange");

        SqlServerIndexBuilderExtensions.IncludeProperties(
            dateRangeIndex,
            sd => new { sd.StripeProductIds, sd.AudienceType });

        builder.ToTable(nameof(SubscriptionDiscount));
    }
}
