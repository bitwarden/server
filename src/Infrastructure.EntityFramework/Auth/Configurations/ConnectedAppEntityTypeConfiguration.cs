using Bit.Infrastructure.EntityFramework.Auth.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.Auth.Configurations;

public class ConnectedAppEntityTypeConfiguration : IEntityTypeConfiguration<ConnectedApp>
{
    public void Configure(EntityTypeBuilder<ConnectedApp> builder)
    {
        builder
            .Property(c => c.Id)
            .ValueGeneratedNever();

        builder
            .HasIndex(c => c.UserId)
            .IsClustered(false);

        // UserId is required here (unlike Send, whose UserId is nullable), so the convention default would be
        // a cascading delete. Pin it to NoAction to match the MSSQL schema, which has a plain User FK.
        builder
            .HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.ToTable(nameof(ConnectedApp));
    }
}
