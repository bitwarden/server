using System.Text.Json;
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

        builder
            .Property(sd => sd.StripeProductIds)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => v == null ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null));

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
