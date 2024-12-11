using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.Configurations;

public class UserEntityTypeConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(u => u.Id).ValueGeneratedNever();

        builder.HasIndex(u => u.Email).IsUnique().IsClustered(false);

        builder
            .HasIndex(u => new
            {
                u.Premium,
                u.PremiumExpirationDate,
                u.RenewalReminderDate,
            })
            .IsClustered(false);

        builder.ToTable(nameof(User));
    }
}
