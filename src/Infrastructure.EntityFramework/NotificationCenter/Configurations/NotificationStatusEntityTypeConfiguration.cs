﻿#nullable enable
using Bit.Infrastructure.EntityFramework.NotificationCenter.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.NotificationCenter.Configurations;

public class NotificationStatusEntityTypeConfiguration : IEntityTypeConfiguration<NotificationStatus>
{
    public void Configure(EntityTypeBuilder<NotificationStatus> builder)
    {
        builder
            .HasKey(ns => new { ns.UserId, ns.NotificationId })
            .IsClustered();

        builder
            .HasOne(ns => ns.Notification)
            .WithMany()
            .HasForeignKey(ns => ns.NotificationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(nameof(NotificationStatus));
    }
}
