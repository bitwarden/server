using Bit.Infrastructure.EntityFramework.SecretsManager.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.SecretsManager.Configurations;

public class AccessPolicyEntityTypeConfiguration : IEntityTypeConfiguration<AccessPolicy>
{
    public void Configure(EntityTypeBuilder<AccessPolicy> builder)
    {
        builder
            .HasDiscriminator<string>("Discriminator")
            .HasValue<UserProjectAccessPolicy>("user_project")
            .HasValue<UserServiceAccountAccessPolicy>("user_service_account")
            .HasValue<GroupProjectAccessPolicy>("group_project")
            .HasValue<GroupServiceAccountAccessPolicy>("group_service_account")
            .HasValue<ServiceAccountProjectAccessPolicy>("service_account_project");

        builder
            .Property(s => s.Id)
            .ValueGeneratedNever();

        builder
            .HasKey(s => s.Id)
            .IsClustered();

        builder.ToTable(nameof(AccessPolicy));
    }
}

public class UserProjectAccessPolicyEntityTypeConfiguration : IEntityTypeConfiguration<UserProjectAccessPolicy>
{
    public void Configure(EntityTypeBuilder<UserProjectAccessPolicy> builder)
    {
        builder
            .Property(e => e.OrganizationUserId)
            .HasColumnName(nameof(UserProjectAccessPolicy.OrganizationUserId));

        builder
            .Property(e => e.GrantedProjectId)
            .HasColumnName(nameof(UserProjectAccessPolicy.GrantedProjectId));

        builder
            .HasOne(e => e.GrantedProject)
            .WithMany(e => e.UserAccessPolicies)
            .HasForeignKey(nameof(UserProjectAccessPolicy.GrantedProjectId))
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class UserServiceAccountAccessPolicyEntityTypeConfiguration : IEntityTypeConfiguration<UserServiceAccountAccessPolicy>
{
    public void Configure(EntityTypeBuilder<UserServiceAccountAccessPolicy> builder)
    {
        builder
            .Property(e => e.OrganizationUserId)
            .HasColumnName(nameof(UserServiceAccountAccessPolicy.OrganizationUserId));

        builder
            .Property(e => e.GrantedServiceAccountId)
            .HasColumnName(nameof(UserServiceAccountAccessPolicy.GrantedServiceAccountId));
    }
}

public class GroupProjectAccessPolicyEntityTypeConfiguration : IEntityTypeConfiguration<GroupProjectAccessPolicy>
{
    public void Configure(EntityTypeBuilder<GroupProjectAccessPolicy> builder)
    {
        builder
            .Property(e => e.GroupId)
            .HasColumnName(nameof(GroupProjectAccessPolicy.GroupId));

        builder
            .Property(e => e.GrantedProjectId)
            .HasColumnName(nameof(GroupProjectAccessPolicy.GrantedProjectId));

        builder
            .HasOne(e => e.GrantedProject)
            .WithMany(e => e.GroupAccessPolicies)
            .HasForeignKey(nameof(GroupProjectAccessPolicy.GrantedProjectId))
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(e => e.Group)
            .WithMany()
            .HasForeignKey(nameof(GroupProjectAccessPolicy.GroupId))
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class GroupServiceAccountAccessPolicyEntityTypeConfiguration : IEntityTypeConfiguration<GroupServiceAccountAccessPolicy>
{
    public void Configure(EntityTypeBuilder<GroupServiceAccountAccessPolicy> builder)
    {
        builder
            .Property(e => e.GroupId)
            .HasColumnName(nameof(GroupServiceAccountAccessPolicy.GroupId));

        builder
            .Property(e => e.GrantedServiceAccountId)
            .HasColumnName(nameof(GroupServiceAccountAccessPolicy.GrantedServiceAccountId));

        builder
            .HasOne(e => e.Group)
            .WithMany()
            .HasForeignKey(nameof(GroupProjectAccessPolicy.GroupId))
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ServiceAccountProjectAccessPolicyEntityTypeConfiguration : IEntityTypeConfiguration<ServiceAccountProjectAccessPolicy>
{
    public void Configure(EntityTypeBuilder<ServiceAccountProjectAccessPolicy> builder)
    {
        builder
            .Property(e => e.ServiceAccountId)
            .HasColumnName(nameof(ServiceAccountProjectAccessPolicy.ServiceAccountId));

        builder
            .Property(e => e.GrantedProjectId)
            .HasColumnName(nameof(ServiceAccountProjectAccessPolicy.GrantedProjectId));

        builder
            .HasOne(e => e.GrantedProject)
            .WithMany(e => e.ServiceAccountAccessPolicies)
            .HasForeignKey(nameof(ServiceAccountProjectAccessPolicy.GrantedProjectId))
            .OnDelete(DeleteBehavior.Cascade);
    }
}
