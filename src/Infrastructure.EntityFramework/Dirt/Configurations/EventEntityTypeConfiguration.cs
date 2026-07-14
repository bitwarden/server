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

        builder.ToTable(nameof(Event));
    }
}
