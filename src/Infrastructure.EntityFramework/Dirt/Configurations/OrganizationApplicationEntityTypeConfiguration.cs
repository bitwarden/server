using Bit.Infrastructure.EntityFramework.Dirt.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.Dirt.Configurations;

public class OrganizationApplicationEntityTypeConfiguration : IEntityTypeConfiguration<OrganizationApplication>
{
    public void Configure(EntityTypeBuilder<OrganizationApplication> builder)
    {
        builder
            .Property(s => s.Id)
            .ValueGeneratedNever();

        builder.HasIndex(s => s.Id)
            .IsClustered(true);

        builder
            .HasIndex(s => s.OrganizationId)
            .IsClustered(false);

        builder
            .HasOne(s => s.Organization)
            .WithMany()
            .HasForeignKey(s => s.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(nameof(OrganizationApplication));
    }
}
