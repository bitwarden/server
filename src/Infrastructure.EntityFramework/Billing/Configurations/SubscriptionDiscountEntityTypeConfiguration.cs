using System.Text.Json;
using Bit.Infrastructure.EntityFramework.Billing.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
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
                v => v == null ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .Metadata.SetValueComparer(new ValueComparer<ICollection<string>?>(
                (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
                c => c == null ? 0 : c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c == null ? null : c.ToList()));

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
