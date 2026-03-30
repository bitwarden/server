using Bit.Infrastructure.EntityFramework.Autofill.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.Autofill.Configurations;

public class AutofillTriageReportEntityTypeConfiguration : IEntityTypeConfiguration<AutofillTriageReport>
{
    public void Configure(EntityTypeBuilder<AutofillTriageReport> builder)
    {
        builder
            .HasIndex(e => new { e.Archived, e.CreationDate })
            .HasDatabaseName("IX_AutofillTriageReport_CreationDate");
    }
}
