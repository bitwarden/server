using Bit.Infrastructure.EntityFramework.Tools.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.Tools.Configurations;

public class ReceiveEntityTypeConfiguration : IEntityTypeConfiguration<Receive>
{
    public void Configure(EntityTypeBuilder<Receive> builder)
    {
        builder
            .Property(r => r.Id)
            .ValueGeneratedNever();

        builder
            .HasIndex(r => r.ExpirationDate)
            .IsClustered(false);

        builder
            .HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.ToTable(nameof(Receive));
    }
}
