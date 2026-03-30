using Bit.Core.Autofill.Entities;
using Xunit;

namespace Bit.Core.Test.Autofill.Entities;

public class AutofillTriageReportTests
{
    [Fact]
    public void SetNewId_SetsNonEmptyGuid()
    {
        var report = new AutofillTriageReport { PageUrl = "https://example.com", ReportData = "{}" };

        report.SetNewId();

        Assert.NotEqual(Guid.Empty, report.Id);
    }

    [Fact]
    public void SetNewId_SetsUniqueIds()
    {
        var report1 = new AutofillTriageReport { PageUrl = "https://example.com", ReportData = "{}" };
        var report2 = new AutofillTriageReport { PageUrl = "https://example.com", ReportData = "{}" };

        report1.SetNewId();
        report2.SetNewId();

        Assert.NotEqual(report1.Id, report2.Id);
    }

    [Fact]
    public void CreationDate_DefaultsToUtcNow()
    {
        var before = DateTime.UtcNow;
        var report = new AutofillTriageReport { PageUrl = "https://example.com", ReportData = "{}" };
        var after = DateTime.UtcNow;

        Assert.InRange(report.CreationDate, before, after);
    }

    [Fact]
    public void Archived_DefaultsToFalse()
    {
        var report = new AutofillTriageReport { PageUrl = "https://example.com", ReportData = "{}" };

        Assert.False(report.Archived);
    }
}
