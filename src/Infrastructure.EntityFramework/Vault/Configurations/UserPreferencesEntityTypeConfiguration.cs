using Bit.Infrastructure.EntityFramework.Vault.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.Vault.Configurations;

public class UserPreferencesEntityTypeConfiguration : IEntityTypeConfiguration<UserPreferences>
{
    public void Configure(EntityTypeBuilder<UserPreferences> builder)
    {
        builder
            .Property(up => up.Id)
            .ValueGeneratedNever();

        builder
            .HasKey(up => up.Id)
            .IsClustered();

        builder
            .HasIndex(up => up.UserId)
            .IsClustered(false);

        builder
            .HasOne(up => up.User)
            .WithMany()
            .HasForeignKey(up => up.UserId);

        builder
            .ToTable(nameof(UserPreferences));
    }
}
