using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.Configurations;

public class EventEntityTypeConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder
            .HasIndex(e => new
            {
                e.Date,
                e.OrganizationId,
                e.ActingUserId,
                e.CipherId,
            })
            .IsClustered(false);

        builder.ToTable(nameof(Event));
    }
}
