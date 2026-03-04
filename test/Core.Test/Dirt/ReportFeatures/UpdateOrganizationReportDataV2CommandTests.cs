using AutoFixture;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Models.Data;
using Bit.Core.Dirt.Reports.ReportFeatures;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Exceptions;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Dirt.ReportFeatures;

[SutProviderCustomize]
public class UpdateOrganizationReportDataV2CommandTests
{
    private static OrganizationReport CreateReportWithFileData(Guid reportId, Guid organizationId, string fileId)
    {
        var fileData = new ReportFile
        {
            Id = fileId,
            FileName = "report-data.json",
            Validated = false
        };

        var report = new OrganizationReport
        {
            Id = reportId,
            OrganizationId = organizationId
        };
        report.SetReportFileData(fileData);
        return report;
    }

    [Theory]
    [BitAutoData]
    public async Task GetUploadUrlAsync_WithMismatchedFileId_ShouldThrowNotFoundException(
        SutProvider<UpdateOrganizationReportDataV2Command> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Create<UpdateOrganizationReportDataRequest>();
        var existingReport = CreateReportWithFileData(request.ReportId, request.OrganizationId, "stored-file-id");

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(request.ReportId)
            .Returns(existingReport);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(async () =>
            await sutProvider.Sut.GetUploadUrlAsync(request, "attacker-supplied-file-id"));

        Assert.Equal("Report not found", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task GetUploadUrlAsync_WithNonExistentReport_ShouldThrowNotFoundException(
        SutProvider<UpdateOrganizationReportDataV2Command> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Create<UpdateOrganizationReportDataRequest>();

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(request.ReportId)
            .Returns((OrganizationReport)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(async () =>
            await sutProvider.Sut.GetUploadUrlAsync(request, "any-file-id"));

        Assert.Equal("Report not found", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task GetUploadUrlAsync_WithMismatchedOrgId_ShouldThrowNotFoundException(
        SutProvider<UpdateOrganizationReportDataV2Command> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Create<UpdateOrganizationReportDataRequest>();
        var existingReport = CreateReportWithFileData(request.ReportId, Guid.NewGuid(), "file-id");

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(request.ReportId)
            .Returns(existingReport);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(async () =>
            await sutProvider.Sut.GetUploadUrlAsync(request, "any-file-id"));
    }
}
