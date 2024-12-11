using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.Tools.Configurations;

public class SendEntityTypeConfiguration : IEntityTypeConfiguration<Send>
{
    public void Configure(EntityTypeBuilder<Send> builder)
    {
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.HasIndex(s => s.UserId).IsClustered(false);

        builder.HasIndex(s => new { s.UserId, s.OrganizationId }).IsClustered(false);

        builder.HasIndex(s => s.DeletionDate).IsClustered(false);

        builder.ToTable(nameof(Send));
    }
}
