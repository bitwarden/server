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

        builder.ToTable(nameof(Event));
    }
}
