using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.Configurations;

public class TransactionEntityTypeConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.HasIndex(t => t.UserId).IsClustered(false);

        builder
            .HasIndex(t => new
            {
                t.UserId,
                t.OrganizationId,
                t.CreationDate,
            })
            .IsClustered(false);

        builder.ToTable(nameof(Transaction));
    }
}
