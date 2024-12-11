using Bit.Infrastructure.EntityFramework.AdminConsole.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Configurations;

public class PolicyEntityTypeConfiguration : IEntityTypeConfiguration<Policy>
{
    public void Configure(EntityTypeBuilder<Policy> builder)
    {
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.HasIndex(p => p.OrganizationId).IsClustered(false);

        builder.HasIndex(p => new { p.OrganizationId, p.Type }).IsUnique().IsClustered(false);

        builder.ToTable(nameof(Policy));
    }
}
