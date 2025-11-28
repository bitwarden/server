using Bit.Infrastructure.EntityFramework.SecretsManager.Discriminators;
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
            .HasValue<UserProjectAccessPolicy>(AccessPolicyDiscriminator.UserProject)
            .HasValue<UserServiceAccountAccessPolicy>(AccessPolicyDiscriminator.UserServiceAccount)
            .HasValue<UserSecretAccessPolicy>(AccessPolicyDiscriminator.UserSecret)
            .HasValue<GroupProjectAccessPolicy>(AccessPolicyDiscriminator.GroupProject)
            .HasValue<GroupServiceAccountAccessPolicy>(AccessPolicyDiscriminator.GroupServiceAccount)
            .HasValue<GroupSecretAccessPolicy>(AccessPolicyDiscriminator.GroupSecret)
            .HasValue<ServiceAccountProjectAccessPolicy>(AccessPolicyDiscriminator.ServiceAccountProject)
            .HasValue<ServiceAccountSecretAccessPolicy>(AccessPolicyDiscriminator.ServiceAccountSecret);

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

public class UserSecretAccessPolicyEntityTypeConfiguration : IEntityTypeConfiguration<UserSecretAccessPolicy>
{
    public void Configure(EntityTypeBuilder<UserSecretAccessPolicy> builder)
    {
        builder
            .Property(e => e.OrganizationUserId)
            .HasColumnName(nameof(UserSecretAccessPolicy.OrganizationUserId));

        builder
            .Property(e => e.GrantedSecretId)
            .HasColumnName(nameof(UserSecretAccessPolicy.GrantedSecretId));

        builder
            .HasOne(e => e.GrantedSecret)
            .WithMany(e => e.UserAccessPolicies)
            .HasForeignKey(nameof(UserSecretAccessPolicy.GrantedSecretId))
            .OnDelete(DeleteBehavior.Cascade);
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

public class GroupSecretAccessPolicyEntityTypeConfiguration : IEntityTypeConfiguration<GroupSecretAccessPolicy>
{
    public void Configure(EntityTypeBuilder<GroupSecretAccessPolicy> builder)
    {
        builder
            .Property(e => e.GroupId)
            .HasColumnName(nameof(GroupSecretAccessPolicy.GroupId));

        builder
            .Property(e => e.GrantedSecretId)
            .HasColumnName(nameof(GroupSecretAccessPolicy.GrantedSecretId));

        builder
            .HasOne(e => e.GrantedSecret)
            .WithMany(e => e.GroupAccessPolicies)
            .HasForeignKey(nameof(GroupSecretAccessPolicy.GrantedSecretId))
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(e => e.Group)
            .WithMany()
            .HasForeignKey(nameof(GroupSecretAccessPolicy.GroupId))
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

public class ServiceAccountSecretAccessPolicyEntityTypeConfiguration : IEntityTypeConfiguration<ServiceAccountSecretAccessPolicy>
{
    public void Configure(EntityTypeBuilder<ServiceAccountSecretAccessPolicy> builder)
    {
        builder
            .Property(e => e.ServiceAccountId)
            .HasColumnName(nameof(ServiceAccountSecretAccessPolicy.ServiceAccountId));

        builder
            .Property(e => e.GrantedSecretId)
            .HasColumnName(nameof(ServiceAccountSecretAccessPolicy.GrantedSecretId));

        builder
            .HasOne(e => e.GrantedSecret)
            .WithMany(e => e.ServiceAccountAccessPolicies)
            .HasForeignKey(nameof(ServiceAccountSecretAccessPolicy.GrantedSecretId))
            .OnDelete(DeleteBehavior.Cascade);
    }
}
