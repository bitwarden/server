using Bit.Infrastructure.EntityFramework.Tools.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.Tools.Configurations;

public class PasswordHealthReportApplicationEntityTypeConfiguration
    : IEntityTypeConfiguration<PasswordHealthReportApplication>
{
    public void Configure(EntityTypeBuilder<PasswordHealthReportApplication> builder)
    {
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.HasIndex(s => s.Id).IsClustered(true);

        builder.HasIndex(s => s.OrganizationId).IsClustered(false);

        builder.ToTable(nameof(PasswordHealthReportApplication));
    }
}
