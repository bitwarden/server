using Bit.Infrastructure.EntityFramework.SecretsManager.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.SecretsManager.Configurations;

public class SecretVersionEntityTypeConfiguration : IEntityTypeConfiguration<SecretVersion>
{
    public void Configure(EntityTypeBuilder<SecretVersion> builder)
    {
        builder.Property(sv => sv.Id)
            .ValueGeneratedNever();

        builder.HasKey(sv => sv.Id)
            .IsClustered();

        builder.Property(sv => sv.Value)
            .IsRequired();

        builder.Property(sv => sv.VersionDate)
            .IsRequired();

        builder.HasOne(sv => sv.EditorServiceAccount)
            .WithMany()
            .HasForeignKey(sv => sv.EditorServiceAccountId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(sv => sv.EditorOrganizationUser)
            .WithMany()
            .HasForeignKey(sv => sv.EditorOrganizationUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(sv => sv.SecretId)
            .HasDatabaseName("IX_SecretVersion_SecretId");

        builder.HasIndex(sv => sv.EditorServiceAccountId)
            .HasDatabaseName("IX_SecretVersion_EditorServiceAccountId");

        builder.HasIndex(sv => sv.EditorOrganizationUserId)
            .HasDatabaseName("IX_SecretVersion_EditorOrganizationUserId");
    }
}
