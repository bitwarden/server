using AutoFixture;
using Azure.Storage.Blobs;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Models.Data;
using Bit.Core.Dirt.Reports.Services;
using Bit.Core.Enums;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Dirt.Reports.Services;

public class AzureOrganizationReportStorageServiceTests
{
    private const string DevConnectionString = "UseDevelopmentStorage=true";

    private static AzureOrganizationReportStorageService CreateSut()
    {
        var blobServiceClient = new BlobServiceClient(DevConnectionString);
        var containerClient = blobServiceClient.GetBlobContainerClient(
            AzureOrganizationReportStorageService.ContainerName);
        var logger = Substitute.For<ILogger<AzureOrganizationReportStorageService>>();
        return new AzureOrganizationReportStorageService(containerClient, logger);
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
        Assert.Equal(FileUploadType.Azure, CreateSut().FileUploadType);
    }

    [Fact]
    public async Task GetReportFileUploadUrlAsync_ReturnsValidSasUrl()
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
        var url = await sut.GetReportFileUploadUrlAsync(report, fileData);

        // Assert
        Assert.NotNull(url);
        Assert.NotEmpty(url);
        Assert.Contains("report-data.json", url);
        Assert.Contains("sig=", url);
        Assert.Contains("se=", url);
        // Upload URL should have create and write permissions
        var uri = new Uri(url);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var permissions = query["sp"];
        Assert.NotNull(permissions);
        Assert.Contains("c", permissions); // Create
        Assert.Contains("w", permissions); // Write
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
        Assert.Contains("sig=", url);
        // Download URL should have read-only permission
        var uri = new Uri(url);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var permissions = query["sp"];
        Assert.NotNull(permissions);
        Assert.Contains("r", permissions); // Read
        Assert.DoesNotContain("w", permissions); // No write
    }

    [Theory]
    [InlineData("orgId/03-02-2026/reportId/fileId/report-data.json", "reportId")]
    [InlineData("abc/01-01-2026/def/ghi/report-data.json", "def")]
    public void ReportIdFromBlobName_ExtractsReportId(string blobName, string expectedReportId)
    {
        var result = AzureOrganizationReportStorageService.ReportIdFromBlobName(blobName);
        Assert.Equal(expectedReportId, result);
    }

    [Fact]
    public void BlobPath_FormatsCorrectly()
    {
        // Arrange
        var fixture = new Fixture();

        var orgId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var reportId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var creationDate = new DateTime(2026, 2, 17);

        var report = fixture.Build<OrganizationReport>()
            .With(r => r.OrganizationId, orgId)
            .With(r => r.Id, reportId)
            .With(r => r.CreationDate, creationDate)
            .With(r => r.ReportData, string.Empty)
            .Create();

        // Act
        var path = AzureOrganizationReportStorageService.BlobPath(report, "abc123xyz", "report-data.json");

        // Assert
        var expectedPath = $"{orgId}/02-17-2026/{reportId}/abc123xyz/report-data.json";
        Assert.Equal(expectedPath, path);
    }
}
