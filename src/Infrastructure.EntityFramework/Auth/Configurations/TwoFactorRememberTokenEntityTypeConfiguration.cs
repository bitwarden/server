using Bit.Infrastructure.EntityFramework.Auth.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.Auth.Configurations;

public class TwoFactorRememberTokenEntityTypeConfiguration : IEntityTypeConfiguration<TwoFactorRememberToken>
{
    public void Configure(EntityTypeBuilder<TwoFactorRememberToken> builder)
    {
        builder
            .Property(e => e.Id)
            .ValueGeneratedNever();

        // Enforces one row per remembered device, serves the point lookup on validation, and — because
        // UserId leads — also serves the revoke-everything-for-this-user write.
        builder
            .HasIndex(e => new { e.UserId, e.DeviceId })
            .IsUnique()
            .IsClustered(false);

        // Foreign keys are not indexed automatically, and the cascade below needs a seek on DeviceId.
        // The composite index above cannot serve it because DeviceId is not its leading column.
        builder
            .HasIndex(e => e.DeviceId)
            .IsClustered(false);

        builder
            .HasIndex(e => e.ExpirationDate)
            .IsClustered(false);

        // Must match the MSSQL schema. The bulk user-delete path uses ExecuteDeleteAsync on Device,
        // which bypasses change tracking, so it relies on this cascade existing in the database.
        builder
            .HasOne(e => e.Device)
            .WithMany()
            .HasForeignKey(e => e.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.ToTable(nameof(TwoFactorRememberToken));
    }
}
