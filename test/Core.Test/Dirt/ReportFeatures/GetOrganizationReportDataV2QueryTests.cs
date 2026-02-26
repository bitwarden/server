using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Enums;
using Bit.Core.Dirt.Models.Data;
using Bit.Core.Dirt.Reports.ReportFeatures;
using Bit.Core.Dirt.Reports.Services;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Exceptions;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Dirt.ReportFeatures;

[SutProviderCustomize]
public class GetOrganizationReportDataV2QueryTests
{
    private static OrganizationReport CreateReportWithFileData(Guid reportId, Guid organizationId, string fileId)
    {
        var fileData = new OrganizationReportFileData
        {
            Id = fileId,
            Validated = true
        };

        var report = new OrganizationReport
        {
            Id = reportId,
            OrganizationId = organizationId,
            Type = OrganizationReportType.File
        };
        report.SetReportFileData(fileData);
        return report;
    }

    [Theory]
    [BitAutoData]
    public async Task GetOrganizationReportDataAsync_Success_ReturnsDownloadUrl(
        SutProvider<GetOrganizationReportDataV2Query> sutProvider)
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var reportFileId = "test-file-id-plaintext";
        var expectedUrl = "https://blob.storage.azure.com/sas-url";

        var report = CreateReportWithFileData(reportId, organizationId, "encrypted-file-id");

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(reportId)
            .Returns(report);

        sutProvider.GetDependency<IOrganizationReportStorageService>()
            .GetReportDataDownloadUrlAsync(report, Arg.Any<OrganizationReportFileData>())
            .Returns(expectedUrl);

        // Act
        var result = await sutProvider.Sut.GetOrganizationReportDataAsync(organizationId, reportId, reportFileId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedUrl, result.DownloadUrl);

        await sutProvider.GetDependency<IOrganizationReportStorageService>()
            .Received(1)
            .GetReportDataDownloadUrlAsync(report, Arg.Any<OrganizationReportFileData>());
    }

    [Theory]
    [BitAutoData]
    public async Task GetOrganizationReportDataAsync_ReportNotFound_ThrowsNotFoundException(
        SutProvider<GetOrganizationReportDataV2Query> sutProvider)
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var reportFileId = "test-file-id";

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(reportId)
            .Returns(null as OrganizationReport);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            async () => await sutProvider.Sut.GetOrganizationReportDataAsync(organizationId, reportId, reportFileId));
    }

    [Theory]
    [BitAutoData]
    public async Task GetOrganizationReportDataAsync_OrganizationMismatch_ThrowsNotFoundException(
        SutProvider<GetOrganizationReportDataV2Query> sutProvider)
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var differentOrgId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var reportFileId = "test-file-id";

        var report = CreateReportWithFileData(reportId, differentOrgId, "file-id");

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(reportId)
            .Returns(report);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            async () => await sutProvider.Sut.GetOrganizationReportDataAsync(organizationId, reportId, reportFileId));
    }

    [Theory]
    [BitAutoData]
    public async Task GetOrganizationReportDataAsync_MissingReportFileId_ThrowsBadRequestException(
        SutProvider<GetOrganizationReportDataV2Query> sutProvider)
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        string? reportFileId = null;

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(
            async () => await sutProvider.Sut.GetOrganizationReportDataAsync(organizationId, reportId, reportFileId!));
    }

    [Theory]
    [BitAutoData]
    public async Task GetOrganizationReportDataAsync_EmptyReportData_ThrowsNotFoundException(
        SutProvider<GetOrganizationReportDataV2Query> sutProvider)
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var reportFileId = "test-file-id";

        var report = new OrganizationReport
        {
            Id = reportId,
            OrganizationId = organizationId,
            ReportData = string.Empty,
            Type = OrganizationReportType.Data
        };

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(reportId)
            .Returns(report);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            async () => await sutProvider.Sut.GetOrganizationReportDataAsync(organizationId, reportId, reportFileId));
    }
}
