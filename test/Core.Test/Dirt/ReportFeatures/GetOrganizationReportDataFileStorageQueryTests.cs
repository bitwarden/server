using AutoFixture;
using Bit.Core.Dirt.Entities;
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
public class GetOrganizationReportDataFileStorageQueryTests
{
    [Theory]
    [BitAutoData]
    public async Task GetOrganizationReportDataAsync_Success_ReturnsDownloadUrl(
        SutProvider<GetOrganizationReportDataFileStorageQuery> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var organizationId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var reportFileId = "test-file-id-plaintext";
        var expectedUrl = "https://blob.storage.azure.com/sas-url";

        var report = fixture.Build<OrganizationReport>()
            .With(r => r.Id, reportId)
            .With(r => r.OrganizationId, organizationId)
            .With(r => r.FileId, "encrypted-file-id")
            .Create();

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(reportId)
            .Returns(report);

        sutProvider.GetDependency<IOrganizationReportStorageService>()
            .GetReportDataDownloadUrlAsync(report, reportFileId)
            .Returns(expectedUrl);

        // Act
        var result = await sutProvider.Sut.GetOrganizationReportDataAsync(organizationId, reportId, reportFileId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedUrl, result.DownloadUrl);

        await sutProvider.GetDependency<IOrganizationReportStorageService>()
            .Received(1)
            .GetReportDataDownloadUrlAsync(report, reportFileId);
    }

    [Theory]
    [BitAutoData]
    public async Task GetOrganizationReportDataAsync_ReportNotFound_ThrowsNotFoundException(
        SutProvider<GetOrganizationReportDataFileStorageQuery> sutProvider)
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
        SutProvider<GetOrganizationReportDataFileStorageQuery> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var organizationId = Guid.NewGuid();
        var differentOrgId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var reportFileId = "test-file-id";

        var report = fixture.Build<OrganizationReport>()
            .With(r => r.Id, reportId)
            .With(r => r.OrganizationId, differentOrgId)
            .Create();

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
        SutProvider<GetOrganizationReportDataFileStorageQuery> sutProvider)
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        string? reportFileId = null;

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(
            async () => await sutProvider.Sut.GetOrganizationReportDataAsync(organizationId, reportId, reportFileId!));
    }
}
