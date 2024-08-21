#nullable enable
using Bit.Infrastructure.EntityFramework.NotificationCenter.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.NotificationCenter.Configurations;

public class NotificationEntityTypeConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder
            .Property(n => n.Id)
            .ValueGeneratedNever();

        builder
            .HasKey(n => n.Id)
            .IsClustered();

        builder
            .HasIndex(n => n.CreationDate)
            .IsDescending(true)
            .IsClustered(false);

        builder
            .HasIndex(n => n.OrganizationId)
            .IsClustered(false);

        builder
            .HasIndex(n => n.UserId)
            .IsClustered(false);

        builder.ToTable(nameof(Notification));
    }
}
