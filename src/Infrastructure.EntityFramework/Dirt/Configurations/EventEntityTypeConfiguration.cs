using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.Configurations;

public class EventEntityTypeConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder
            .Property(e => e.Id)
            .ValueGeneratedNever();

        builder.HasKey(e => e.Id)
            .IsClustered();

        var index = builder.HasIndex(e => new { e.Date, e.OrganizationId, e.ActingUserId, e.CipherId })
            .IsClustered(false)
            .HasDatabaseName("IX_Event_DateOrganizationIdUserId");

        SqlServerIndexBuilderExtensions.IncludeProperties(
            index,
            e => new { e.ServiceAccountId, e.GrantedServiceAccountId });

        // Supports bulk deletion of an organization's events (Event_DeleteManyByOrganizationId).
        // Not filtered here because MySQL does not support filtered indexes; MSSQL filters on
        // OrganizationId IS NOT NULL to keep the index small since many events are user-scoped.
        builder
            .HasIndex(e => e.OrganizationId)
            .IsClustered(false)
            .HasDatabaseName("IX_Event_OrganizationId");

        builder.ToTable(nameof(Event));
    }
}
