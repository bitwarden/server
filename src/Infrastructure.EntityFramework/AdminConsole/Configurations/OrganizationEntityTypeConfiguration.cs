using Bit.Infrastructure.EntityFramework.AdminConsole.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Configurations;

public class OrganizationEntityTypeConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder
            .Property(o => o.Id)
            .ValueGeneratedNever();

        builder.Property(c => c.AllowAdminAccessToAllCollectionItems)
            .ValueGeneratedNever()
            .HasDefaultValue(true);

        NpgsqlIndexBuilderExtensions.IncludeProperties(
            builder.HasIndex(o => new { o.Id, o.Enabled }),
            o => new { o.UseTotp, o.UsersGetPremium });

        // The key id is written on insert and by SetOrganizationKeyId only, to match Organization_Update.
        // ReplaceAsync and other tracked updates ignore it, so a stale organization read cannot overwrite it.
        builder.Property(o => o.OrganizationKeyId)
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);

        builder.HasIndex(o => o.GatewayCustomerId);
        builder.HasIndex(o => o.GatewaySubscriptionId);

        builder.ToTable(nameof(Organization));
    }
}
