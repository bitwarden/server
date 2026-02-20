using AutoFixture;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Reports.Services;
using Bit.Core.Enums;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.Dirt.Reports.Services;

[SutProviderCustomize]
public class AzureOrganizationReportStorageServiceTests
{
    private static Core.Settings.GlobalSettings GetGlobalSettings()
    {
        var globalSettings = new Core.Settings.GlobalSettings();
        globalSettings.OrganizationReport.ConnectionString = "UseDevelopmentStorage=true";
        return globalSettings;
    }

    [Fact]
    public void FileUploadType_ReturnsAzure()
    {
        // Arrange
        var globalSettings = GetGlobalSettings();
        var sut = new AzureOrganizationReportStorageService(globalSettings);

        // Act & Assert
        Assert.Equal(FileUploadType.Azure, sut.FileUploadType);
    }

    [Fact]
    public async Task GetReportDataUploadUrlAsync_ReturnsValidSasUrl()
    {
        // Arrange
        var fixture = new Fixture();
        var globalSettings = GetGlobalSettings();
        var sut = new AzureOrganizationReportStorageService(globalSettings);

        var report = fixture.Build<OrganizationReport>()
            .With(r => r.OrganizationId, Guid.NewGuid())
            .With(r => r.Id, Guid.NewGuid())
            .With(r => r.CreationDate, new DateTime(2026, 2, 17))
            .Create();

        var reportFileId = "test-file-id-123";

        // Act
        var url = await sut.GetReportDataUploadUrlAsync(report, reportFileId);

        // Assert
        Assert.NotNull(url);
        Assert.NotEmpty(url);
        Assert.Contains("report-data.json", url);
        Assert.Contains("sig=", url); // SAS signature
        Assert.Contains("sp=", url); // Permissions
        Assert.Contains("se=", url); // Expiry
    }

    [Fact]
    public async Task GetReportDataDownloadUrlAsync_ReturnsValidSasUrl()
    {
        // Arrange
        var fixture = new Fixture();
        var globalSettings = GetGlobalSettings();
        var sut = new AzureOrganizationReportStorageService(globalSettings);

        var report = fixture.Build<OrganizationReport>()
            .With(r => r.OrganizationId, Guid.NewGuid())
            .With(r => r.Id, Guid.NewGuid())
            .With(r => r.CreationDate, new DateTime(2026, 2, 17))
            .Create();

        var reportFileId = "test-file-id-123";

        // Act
        var url = await sut.GetReportDataDownloadUrlAsync(report, reportFileId);

        // Assert
        Assert.NotNull(url);
        Assert.NotEmpty(url);
        Assert.Contains("report-data.json", url);
        Assert.Contains("sig=", url); // SAS signature
        Assert.Contains("sp=", url); // Permissions (should be read-only)
    }

    [Fact]
    public async Task BlobPath_FormatsCorrectly()
    {
        // Arrange
        var fixture = new Fixture();
        var globalSettings = GetGlobalSettings();
        var sut = new AzureOrganizationReportStorageService(globalSettings);

        var orgId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var reportId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var creationDate = new DateTime(2026, 2, 17);
        var reportFileId = "abc123xyz";

        var report = fixture.Build<OrganizationReport>()
            .With(r => r.OrganizationId, orgId)
            .With(r => r.Id, reportId)
            .With(r => r.CreationDate, creationDate)
            .Create();

        // Act
        var url = await sut.GetReportDataUploadUrlAsync(report, reportFileId);

        // Assert
        // Expected path: {orgId}/{MM-dd-yyyy}/{reportId}/{fileId}/report-data.json
        var expectedPath = $"{orgId}/02-17-2026/{reportId}/{reportFileId}/report-data.json";
        Assert.Contains(expectedPath, url);
    }
}
