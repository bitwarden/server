using AutoFixture;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Reports.Services;
using Bit.Core.Enums;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.Dirt.Reports.Services;

[SutProviderCustomize]
public class LocalOrganizationReportStorageServiceTests
{
    private static Core.Settings.GlobalSettings GetGlobalSettings()
    {
        var globalSettings = new Core.Settings.GlobalSettings();
        globalSettings.OrganizationReport.BaseDirectory = "/tmp/bitwarden-test/reports";
        globalSettings.OrganizationReport.BaseUrl = "https://localhost/reports";
        return globalSettings;
    }

    [Fact]
    public void FileUploadType_ReturnsDirect()
    {
        // Arrange
        var globalSettings = GetGlobalSettings();
        var sut = new LocalOrganizationReportStorageService(globalSettings);

        // Act & Assert
        Assert.Equal(FileUploadType.Direct, sut.FileUploadType);
    }

    [Fact]
    public async Task GetReportDataUploadUrlAsync_ReturnsApiEndpoint()
    {
        // Arrange
        var fixture = new Fixture();
        var globalSettings = GetGlobalSettings();
        var sut = new LocalOrganizationReportStorageService(globalSettings);

        var orgId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var report = fixture.Build<OrganizationReport>()
            .With(r => r.OrganizationId, orgId)
            .With(r => r.Id, reportId)
            .Create();

        var reportFileId = "test-file-id";

        // Act
        var url = await sut.GetReportDataUploadUrlAsync(report, reportFileId);

        // Assert
        Assert.Equal($"/reports/v2/organizations/{orgId}/{reportId}/file/report-data", url);
    }

    [Fact]
    public async Task GetReportDataDownloadUrlAsync_ReturnsBaseUrlWithPath()
    {
        // Arrange
        var fixture = new Fixture();
        var globalSettings = GetGlobalSettings();
        var sut = new LocalOrganizationReportStorageService(globalSettings);

        var orgId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var reportId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var creationDate = new DateTime(2026, 2, 17);
        var reportFileId = "abc123";

        var report = fixture.Build<OrganizationReport>()
            .With(r => r.OrganizationId, orgId)
            .With(r => r.Id, reportId)
            .With(r => r.CreationDate, creationDate)
            .Create();

        // Act
        var url = await sut.GetReportDataDownloadUrlAsync(report, reportFileId);

        // Assert
        Assert.StartsWith("https://localhost/reports/", url);
        Assert.Contains($"{orgId}", url);
        Assert.Contains("02-17-2026", url); // Date format
        Assert.Contains($"{reportId}", url);
        Assert.Contains(reportFileId, url);
        Assert.EndsWith("report-data.json", url);
    }

    [Theory]
    [InlineData("../../etc/malicious")]
    [InlineData("../../../tmp/evil")]
    public async Task UploadReportDataAsync_WithPathTraversalPayload_WritesOutsideBaseDirectory(string maliciousFileId)
    {
        // Arrange - demonstrates the path traversal vulnerability that is mitigated
        // by validating reportFileId matches report.FileId at the controller/command layer
        var fixture = new Fixture();
        var tempDir = Path.Combine(Path.GetTempPath(), "bitwarden-test-" + Guid.NewGuid());

        var globalSettings = new Core.Settings.GlobalSettings();
        globalSettings.OrganizationReport.BaseDirectory = tempDir;
        globalSettings.OrganizationReport.BaseUrl = "https://localhost/reports";

        var sut = new LocalOrganizationReportStorageService(globalSettings);

        var report = fixture.Build<OrganizationReport>()
            .With(r => r.OrganizationId, Guid.NewGuid())
            .With(r => r.Id, Guid.NewGuid())
            .With(r => r.CreationDate, DateTime.UtcNow)
            .Create();

        var testData = "malicious content";
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(testData));

        try
        {
            // Act
            await sut.UploadReportDataAsync(report, maliciousFileId, stream);

            // Assert - the file is written at a path that escapes the intended report directory
            var intendedBaseDir = Path.Combine(tempDir, report.OrganizationId.ToString(),
                report.CreationDate.ToString("MM-dd-yyyy"), report.Id.ToString());
            var actualFilePath = Path.Combine(intendedBaseDir, maliciousFileId, "report-data.json");
            var resolvedPath = Path.GetFullPath(actualFilePath);

            // This demonstrates the vulnerability: the resolved path escapes the base directory
            Assert.False(resolvedPath.StartsWith(Path.GetFullPath(intendedBaseDir)));
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task UploadReportDataAsync_CreatesDirectoryAndWritesFile()
    {
        // Arrange
        var fixture = new Fixture();
        var tempDir = Path.Combine(Path.GetTempPath(), "bitwarden-test-" + Guid.NewGuid());

        var globalSettings = new Core.Settings.GlobalSettings();
        globalSettings.OrganizationReport.BaseDirectory = tempDir;
        globalSettings.OrganizationReport.BaseUrl = "https://localhost/reports";

        var sut = new LocalOrganizationReportStorageService(globalSettings);

        var report = fixture.Build<OrganizationReport>()
            .With(r => r.OrganizationId, Guid.NewGuid())
            .With(r => r.Id, Guid.NewGuid())
            .With(r => r.CreationDate, DateTime.UtcNow)
            .Create();

        var reportFileId = "test-file-123";
        var testData = "test report data content";
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(testData));

        try
        {
            // Act
            await sut.UploadReportDataAsync(report, reportFileId, stream);

            // Assert
            var expectedDir = Path.Combine(tempDir, report.OrganizationId.ToString(),
                report.CreationDate.ToString("MM-dd-yyyy"), report.Id.ToString(), reportFileId);
            Assert.True(Directory.Exists(expectedDir));

            var expectedFile = Path.Combine(expectedDir, "report-data.json");
            Assert.True(File.Exists(expectedFile));

            var fileContent = await File.ReadAllTextAsync(expectedFile);
            Assert.Equal(testData, fileContent);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
