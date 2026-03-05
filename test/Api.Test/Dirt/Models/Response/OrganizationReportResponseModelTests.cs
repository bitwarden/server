using Bit.Api.Dirt.Models.Response;
using Bit.Core.Dirt.Entities;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Api.Test.Dirt.Models.Response;

public class OrganizationReportResponseModelTests
{
    [Theory, BitAutoData]
    public void Constructor_MapsPropertiesFromEntity(OrganizationReport report)
    {
        report.ReportFile = null;
        var model = new OrganizationReportResponseModel(report);

        Assert.Equal(report.Id, model.Id);
        Assert.Equal(report.OrganizationId, model.OrganizationId);
        Assert.Equal(report.ReportData, model.ReportData);
        Assert.Equal(report.ContentEncryptionKey, model.ContentEncryptionKey);
        Assert.Equal(report.SummaryData, model.SummaryData);
        Assert.Equal(report.ApplicationData, model.ApplicationData);
        Assert.Equal(report.PasswordCount, model.PasswordCount);
        Assert.Equal(report.PasswordAtRiskCount, model.PasswordAtRiskCount);
        Assert.Equal(report.MemberCount, model.MemberCount);
        Assert.Equal(report.CreationDate, model.CreationDate);
        Assert.Equal(report.RevisionDate, model.RevisionDate);
    }

    [Theory, BitAutoData]
    public void Constructor_FileIsNull(OrganizationReport report)
    {
        report.ReportFile = null;
        var model = new OrganizationReportResponseModel(report);

        Assert.Null(model.ReportFile);
    }
}
