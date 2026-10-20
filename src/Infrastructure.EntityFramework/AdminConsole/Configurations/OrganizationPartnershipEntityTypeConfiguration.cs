using Bit.Infrastructure.EntityFramework.AdminConsole.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Configurations;

public class OrganizationPartnershipEntityTypeConfiguration : IEntityTypeConfiguration<OrganizationPartnership>
{
    public void Configure(EntityTypeBuilder<OrganizationPartnership> builder)
    {
        builder
            .Property(e => e.Id)
            .ValueGeneratedNever();

        builder
            .HasOne<Organization>()
            .WithMany()
            .HasForeignKey(e => e.OrganizationId)
            .OnDelete(DeleteBehavior.NoAction);

        builder
            .HasIndex(e => e.OrganizationId)
            .IsUnique()
            .IsClustered(false);

        builder.ToTable(nameof(OrganizationPartnership));
    }
}
