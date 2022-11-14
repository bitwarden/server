using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.Configurations;

public class AccessPolicyEntityTypeConfiguration : IEntityTypeConfiguration<AccessPolicy>
{
    public void Configure(EntityTypeBuilder<AccessPolicy> builder)
    {
        builder
            .Property(s => s.Id)
            .ValueGeneratedNever();

        builder
            .HasKey(s => s.Id)
            .IsClustered();

        builder
            .HasIndex(s => s.OrganizationUserId)
            .IsClustered(false);

        builder
            .HasIndex(s => s.GroupId)
            .IsClustered(false);

        builder
            .HasIndex(s => s.ServiceAccountId)
            .IsClustered(false);

        builder
            .HasIndex(s => s.GrantedProjectId)
            .IsClustered(false);

        builder
            .HasIndex(s => s.GrantedServiceAccountId)
            .IsClustered(false);

        builder.ToTable(nameof(AccessPolicy));
    }
}
