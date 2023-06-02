using Bit.Infrastructure.EntityFramework.Auth.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.Auth.Configurations;

public class GrantEntityTypeConfiguration : IEntityTypeConfiguration<Grant>
{
    public void Configure(EntityTypeBuilder<Grant> builder)
    {
        builder.HasKey(g => g.Key);

        builder
            .HasIndex(g => new { g.SubjectId, g.ClientId, g.Type })
            .IsClustered(false);

        builder
            .HasIndex(g => new { g.SubjectId, g.SessionId, g.Type })
            .IsClustered(false);

        builder
            .HasIndex(g => g.ExpirationDate)
            .IsClustered(false);

        builder.ToTable(nameof(Grant));
    }
}
