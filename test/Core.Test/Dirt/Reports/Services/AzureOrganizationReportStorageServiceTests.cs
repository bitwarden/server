using AutoFixture;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Models.Data;
using Bit.Core.Dirt.Reports.Services;
using Bit.Core.Enums;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Dirt.Reports.Services;

[SutProviderCustomize]
public class AzureOrganizationReportStorageServiceTests
{
    private static AzureOrganizationReportStorageService CreateSut()
    {
        var globalSettings = new Core.Settings.GlobalSettings();
        globalSettings.OrganizationReport.ConnectionString = "UseDevelopmentStorage=true";
        var logger = Substitute.For<ILogger<AzureOrganizationReportStorageService>>();
        return new AzureOrganizationReportStorageService(globalSettings, logger);
    }

    private static ReportFile CreateFileData(string fileId = "test-file-id-123")
    {
        return new ReportFile
        {
            Id = fileId,
            FileName = "report-data.json",
            Validated = false
        };
    }

    [Fact]
    public void FileUploadType_ReturnsAzure()
    {
        // Arrange & Act & Assert
        Assert.Equal(FileUploadType.Azure, CreateSut().FileUploadType);
    }

    [Fact]
    public async Task GetReportDataUploadUrlAsync_ReturnsValidSasUrl()
    {
        // Arrange
        var fixture = new Fixture();
        var sut = CreateSut();

        var report = fixture.Build<OrganizationReport>()
            .With(r => r.OrganizationId, Guid.NewGuid())
            .With(r => r.Id, Guid.NewGuid())
            .With(r => r.CreationDate, new DateTime(2026, 2, 17))
            .With(r => r.ReportData, string.Empty)
            .Create();

        var fileData = CreateFileData();

        // Act
        var url = await sut.GetReportDataUploadUrlAsync(report, fileData);

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
        var sut = CreateSut();

        var report = fixture.Build<OrganizationReport>()
            .With(r => r.OrganizationId, Guid.NewGuid())
            .With(r => r.Id, Guid.NewGuid())
            .With(r => r.CreationDate, new DateTime(2026, 2, 17))
            .With(r => r.ReportData, string.Empty)
            .Create();

        var fileData = CreateFileData();

        // Act
        var url = await sut.GetReportDataDownloadUrlAsync(report, fileData);

        // Assert
        Assert.NotNull(url);
        Assert.NotEmpty(url);
        Assert.Contains("report-data.json", url);
        Assert.Contains("sig=", url); // SAS signature
        Assert.Contains("sp=", url); // Permissions (should be read-only)
    }

    [Theory]
    [InlineData("orgId/03-02-2026/reportId/fileId/report-data.json", "reportId")]
    [InlineData("abc/01-01-2026/def/ghi/report-data.json", "def")]
    public void ReportIdFromBlobName_ExtractsReportId(string blobName, string expectedReportId)
    {
        // Act
        var result = AzureOrganizationReportStorageService.ReportIdFromBlobName(blobName);

        // Assert
        Assert.Equal(expectedReportId, result);
    }

    [Fact]
    public async Task BlobPath_FormatsCorrectly()
    {
        // Arrange
        var fixture = new Fixture();
        var sut = CreateSut();

        var orgId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var reportId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var creationDate = new DateTime(2026, 2, 17);
        var fileData = CreateFileData("abc123xyz");

        var report = fixture.Build<OrganizationReport>()
            .With(r => r.OrganizationId, orgId)
            .With(r => r.Id, reportId)
            .With(r => r.CreationDate, creationDate)
            .With(r => r.ReportData, string.Empty)
            .Create();

        // Act
        var url = await sut.GetReportDataUploadUrlAsync(report, fileData);

        // Assert
        // Expected path: {orgId}/{MM-dd-yyyy}/{reportId}/{fileId}/report-data.json
        var expectedPath = $"{orgId}/02-17-2026/{reportId}/{fileData.Id}/report-data.json";
        Assert.Contains(expectedPath, url);
    }
}
