using AutoFixture;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Reports.ReportFeatures;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Dirt.ReportFeatures;

[SutProviderCustomize]
public class UpdateOrganizationReportV2CommandTests
{
    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_Success_UpdatesFieldsAndReturnsReport(
        SutProvider<UpdateOrganizationReportV2Command> sutProvider)
    {
        var fixture = new Fixture();
        var orgId = Guid.NewGuid();
        var reportId = Guid.NewGuid();

        var existingReport = fixture.Build<OrganizationReport>()
            .With(r => r.Id, reportId)
            .With(r => r.OrganizationId, orgId)
            .Without(r => r.ReportFile)
            .Create();

        var request = new UpdateOrganizationReportV2Request
        {
            ReportId = reportId,
            OrganizationId = orgId,
            ContentEncryptionKey = "new-key",
            SummaryData = "new-summary",
            ApplicationData = "new-app-data",
            ReportMetrics = new OrganizationReportMetrics { ApplicationCount = 10 }
        };

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(orgId)
            .Returns(fixture.Create<Organization>());

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(reportId)
            .Returns(existingReport);

        var result = await sutProvider.Sut.UpdateAsync(request);

        Assert.NotNull(result);
        Assert.Equal("new-key", result.ContentEncryptionKey);
        Assert.Equal("new-summary", result.SummaryData);
        Assert.Equal("new-app-data", result.ApplicationData);

        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .Received(1)
            .ReplaceAsync(Arg.Is<OrganizationReport>(r =>
                r.Id == reportId &&
                r.ContentEncryptionKey == "new-key" &&
                r.SummaryData == "new-summary" &&
                r.ApplicationData == "new-app-data"));
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_WithMetrics_UpdatesMetricFields(
        SutProvider<UpdateOrganizationReportV2Command> sutProvider)
    {
        var fixture = new Fixture();
        var orgId = Guid.NewGuid();
        var reportId = Guid.NewGuid();

        var existingReport = fixture.Build<OrganizationReport>()
            .With(r => r.Id, reportId)
            .With(r => r.OrganizationId, orgId)
            .Without(r => r.ReportFile)
            .Create();

        var metrics = new OrganizationReportMetrics
        {
            ApplicationCount = 100,
            ApplicationAtRiskCount = 10,
            MemberCount = 50,
            MemberAtRiskCount = 5,
            PasswordCount = 200,
            PasswordAtRiskCount = 20
        };

        var request = new UpdateOrganizationReportV2Request
        {
            ReportId = reportId,
            OrganizationId = orgId,
            ContentEncryptionKey = "key",
            SummaryData = "summary",
            ApplicationData = "app-data",
            ReportMetrics = metrics
        };

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(orgId)
            .Returns(fixture.Create<Organization>());

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(reportId)
            .Returns(existingReport);

        var result = await sutProvider.Sut.UpdateAsync(request);

        Assert.Equal(100, result.ApplicationCount);
        Assert.Equal(10, result.ApplicationAtRiskCount);
        Assert.Equal(50, result.MemberCount);
        Assert.Equal(5, result.MemberAtRiskCount);
        Assert.Equal(200, result.PasswordCount);
        Assert.Equal(20, result.PasswordAtRiskCount);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_NullFields_PreservesExistingValues(
        SutProvider<UpdateOrganizationReportV2Command> sutProvider)
    {
        var fixture = new Fixture();
        var orgId = Guid.NewGuid();
        var reportId = Guid.NewGuid();

        var existingReport = fixture.Build<OrganizationReport>()
            .With(r => r.Id, reportId)
            .With(r => r.OrganizationId, orgId)
            .With(r => r.ContentEncryptionKey, "original-key")
            .With(r => r.SummaryData, "original-summary")
            .With(r => r.ApplicationData, "original-app-data")
            .With(r => r.ApplicationCount, 5)
            .Without(r => r.ReportFile)
            .Create();

        var request = new UpdateOrganizationReportV2Request
        {
            ReportId = reportId,
            OrganizationId = orgId,
            ContentEncryptionKey = null,
            SummaryData = null,
            ApplicationData = null,
            ReportMetrics = null
        };

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(orgId)
            .Returns(fixture.Create<Organization>());

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(reportId)
            .Returns(existingReport);

        var result = await sutProvider.Sut.UpdateAsync(request);

        Assert.Equal("original-key", result.ContentEncryptionKey);
        Assert.Equal("original-summary", result.SummaryData);
        Assert.Equal("original-app-data", result.ApplicationData);
        Assert.Equal(5, result.ApplicationCount);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_PartialUpdate_OnlyUpdatesProvidedFields(
        SutProvider<UpdateOrganizationReportV2Command> sutProvider)
    {
        var fixture = new Fixture();
        var orgId = Guid.NewGuid();
        var reportId = Guid.NewGuid();

        var existingReport = fixture.Build<OrganizationReport>()
            .With(r => r.Id, reportId)
            .With(r => r.OrganizationId, orgId)
            .With(r => r.ContentEncryptionKey, "original-key")
            .With(r => r.SummaryData, "original-summary")
            .With(r => r.ApplicationData, "original-app-data")
            .Without(r => r.ReportFile)
            .Create();

        var request = new UpdateOrganizationReportV2Request
        {
            ReportId = reportId,
            OrganizationId = orgId,
            SummaryData = "updated-summary",
            ContentEncryptionKey = null,
            ApplicationData = null,
            ReportMetrics = null
        };

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(orgId)
            .Returns(fixture.Create<Organization>());

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(reportId)
            .Returns(existingReport);

        var result = await sutProvider.Sut.UpdateAsync(request);

        Assert.Equal("original-key", result.ContentEncryptionKey);
        Assert.Equal("updated-summary", result.SummaryData);
        Assert.Equal("original-app-data", result.ApplicationData);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_InvalidOrganization_ThrowsBadRequestException(
        SutProvider<UpdateOrganizationReportV2Command> sutProvider)
    {
        var request = new UpdateOrganizationReportV2Request
        {
            ReportId = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid()
        };

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(request.OrganizationId)
            .Returns(null as Organization);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            async () => await sutProvider.Sut.UpdateAsync(request));
        Assert.Equal("Invalid Organization", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_ReportNotFound_ThrowsNotFoundException(
        SutProvider<UpdateOrganizationReportV2Command> sutProvider)
    {
        var fixture = new Fixture();
        var request = new UpdateOrganizationReportV2Request
        {
            ReportId = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            ContentEncryptionKey = "key",
            SummaryData = "summary",
            ApplicationData = "app-data",
            ReportMetrics = new OrganizationReportMetrics()
        };

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(request.OrganizationId)
            .Returns(fixture.Create<Organization>());

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(request.ReportId)
            .Returns(null as OrganizationReport);

        await Assert.ThrowsAsync<NotFoundException>(
            async () => await sutProvider.Sut.UpdateAsync(request));
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_OrgMismatch_ThrowsBadRequestException(
        SutProvider<UpdateOrganizationReportV2Command> sutProvider)
    {
        var fixture = new Fixture();
        var request = new UpdateOrganizationReportV2Request
        {
            ReportId = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            ContentEncryptionKey = "key",
            SummaryData = "summary",
            ApplicationData = "app-data",
            ReportMetrics = new OrganizationReportMetrics()
        };

        var existingReport = fixture.Build<OrganizationReport>()
            .With(r => r.Id, request.ReportId)
            .With(r => r.OrganizationId, Guid.NewGuid()) // different org
            .Without(r => r.ReportFile)
            .Create();

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(request.OrganizationId)
            .Returns(fixture.Create<Organization>());

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(request.ReportId)
            .Returns(existingReport);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            async () => await sutProvider.Sut.UpdateAsync(request));
        Assert.Equal("Organization report does not belong to the specified organization", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_EmptyOrganizationId_ThrowsBadRequestException(
        SutProvider<UpdateOrganizationReportV2Command> sutProvider)
    {
        var request = new UpdateOrganizationReportV2Request
        {
            ReportId = Guid.NewGuid(),
            OrganizationId = Guid.Empty
        };

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            async () => await sutProvider.Sut.UpdateAsync(request));
        Assert.Equal("Invalid Organization", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_EmptyReportId_ThrowsBadRequestException(
        SutProvider<UpdateOrganizationReportV2Command> sutProvider)
    {
        var request = new UpdateOrganizationReportV2Request
        {
            ReportId = Guid.Empty,
            OrganizationId = Guid.NewGuid()
        };

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            async () => await sutProvider.Sut.UpdateAsync(request));
        Assert.Equal("Invalid Organization", exception.Message);
    }
}
