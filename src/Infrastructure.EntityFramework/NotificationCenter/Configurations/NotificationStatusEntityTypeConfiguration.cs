#nullable enable
using Bit.Infrastructure.EntityFramework.NotificationCenter.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.NotificationCenter.Configurations;

public class NotificationStatusEntityTypeConfiguration : IEntityTypeConfiguration<NotificationStatus>
{
    public void Configure(EntityTypeBuilder<NotificationStatus> builder)
    {
        builder
            .HasKey(ns => new { ns.NotificationId, ns.UserId })
            .IsClustered();

        builder
            .HasIndex(ns => ns.UserId)
            .IsClustered(false);

        builder.ToTable(nameof(NotificationStatus));
    }
}
