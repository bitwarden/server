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

        // Supports reading a single Send's events (Event_ReadPageBySendId). Not filtered here because
        // MySQL does not support filtered indexes; MSSQL filters on
        // SendId IS NOT NULL to keep the index small.
        builder.HasIndex(e => new { e.OrganizationId, e.SendId, e.Date })
            .IsClustered(false)
            .HasDatabaseName("IX_Event_OrganizationIdSendIdDate");

        // Supports bulk deletion of an organization's events (Event_DeleteManyByOrganizationId).
        // Not filtered here because MySQL does not support filtered indexes; MSSQL filters on
        // OrganizationId IS NOT NULL to keep the index small since many events are user-scoped.
        // Only MSSQL requires it, where IX_Event_OrganizationIdSendIdDate is filtered on
        // SendId IS NOT NULL and so cannot serve an unfiltered organization lookup. On the EF
        // providers that composite index is unfiltered with OrganizationId leftmost, making this
        // one redundant there; it is kept regardless so the index is defined once for every
        // provider, and can be dropped here if Event write volume ever makes it worth diverging.
        builder
            .HasIndex(e => e.OrganizationId)
            .IsClustered(false)
            .HasDatabaseName("IX_Event_OrganizationId");

        builder.ToTable(nameof(Event));
    }
}
