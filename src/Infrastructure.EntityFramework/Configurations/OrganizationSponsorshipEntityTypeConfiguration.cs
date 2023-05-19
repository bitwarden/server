using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.Configurations;

public class OrganizationSponsorshipEntityTypeConfiguration : IEntityTypeConfiguration<OrganizationSponsorship>
{
    public void Configure(EntityTypeBuilder<OrganizationSponsorship> builder)
    {
        builder
            .Property(o => o.Id)
            .ValueGeneratedNever();

        builder
            .HasIndex(o => o.SponsoringOrganizationId)
            .HasFilter($"{nameof(OrganizationSponsorship.SponsoringOrganizationId)} IS NOT NULL")
            .IsClustered(false);

        builder
            .HasIndex(o => o.SponsoringOrganizationUserId)
            .IsClustered(false);

        builder
            .HasIndex(o => o.OfferedToEmail)
            .HasFilter($"{nameof(OrganizationSponsorship.OfferedToEmail)} IS NOT NULL")
            .IsClustered(false);

        builder
            .HasIndex(o => o.SponsoredOrganizationId)
            .HasFilter($"{nameof(OrganizationSponsorship.SponsoredOrganizationId)} IS NOT NULL")
            .IsClustered(false);

        builder.ToTable(nameof(OrganizationSponsorship));
    }
}
