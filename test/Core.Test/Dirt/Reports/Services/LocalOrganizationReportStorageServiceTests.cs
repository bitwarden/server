using AutoFixture;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Models.Data;
using Bit.Core.Dirt.Reports.Services;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.DataProtection;
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
        globalSettings.BaseServiceUri.Api = "https://localhost/api";
        return globalSettings;
    }

    private static IDataProtectionProvider GetDataProtectionProvider()
    {
        return DataProtectionProvider.Create("Testing");
    }

    private static LocalOrganizationReportStorageService CreateSut(
        Core.Settings.GlobalSettings? globalSettings = null,
        IDataProtectionProvider? dataProtectionProvider = null)
    {
        return new LocalOrganizationReportStorageService(
            globalSettings ?? GetGlobalSettings(),
            dataProtectionProvider ?? GetDataProtectionProvider());
    }

    private static ReportFile CreateFileData(string fileId = "test-file-id")
    {
        return new ReportFile
        {
            Id = fileId,
            FileName = "report-data.json",
            Validated = false
        };
    }

    [Fact]
    public void FileUploadType_ReturnsDirect()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert
        Assert.Equal(FileUploadType.Direct, sut.FileUploadType);
    }

    [Fact]
    public async Task GetReportFileUploadUrlAsync_ReturnsApiEndpoint()
    {
        // Arrange
        var fixture = new Fixture();
        var sut = CreateSut();

        var orgId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var report = fixture.Build<OrganizationReport>()
            .With(r => r.OrganizationId, orgId)
            .With(r => r.Id, reportId)
            .With(r => r.ReportData, string.Empty)
            .Create();

        var fileData = CreateFileData();

        // Act
        var url = await sut.GetReportFileUploadUrlAsync(report, fileData);

        // Assert
        Assert.Equal($"/reports/organizations/{orgId}/{reportId}/file/report-data", url);
    }

    [Fact]
    public async Task GetReportDataDownloadUrlAsync_ReturnsTokenBasedUrl()
    {
        // Arrange
        var fixture = new Fixture();
        var sut = CreateSut();

        var orgId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var reportId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var creationDate = new DateTime(2026, 2, 17);
        var fileData = CreateFileData("abc123");

        var report = fixture.Build<OrganizationReport>()
            .With(r => r.OrganizationId, orgId)
            .With(r => r.Id, reportId)
            .With(r => r.CreationDate, creationDate)
            .With(r => r.ReportData, string.Empty)
            .Create();

        // Act
        var url = await sut.GetReportDataDownloadUrlAsync(report, fileData);

        // Assert
        Assert.StartsWith("https://localhost/api/reports/organizations/download?token=", url);
    }

    [Fact]
    public async Task ParseReportDownloadToken_RoundTrips()
    {
        // Arrange
        var fixture = new Fixture();
        var dataProtectionProvider = GetDataProtectionProvider();
        var sut = CreateSut(dataProtectionProvider: dataProtectionProvider);

        var reportId = Guid.NewGuid();
        var fileId = "test-file-id";
        var fileData = CreateFileData(fileId);

        var report = fixture.Build<OrganizationReport>()
            .With(r => r.Id, reportId)
            .With(r => r.ReportData, string.Empty)
            .Create();

        // Act - generate a token then parse it
        var url = await sut.GetReportDataDownloadUrlAsync(report, fileData);
        var token = Uri.UnescapeDataString(url.Split("token=")[1]);
        var (parsedReportId, parsedFileId) = sut.ParseReportDownloadToken(token);

        // Assert
        Assert.Equal(reportId, parsedReportId);
        Assert.Equal(fileId, parsedFileId);
    }

    [Fact]
    public void ParseReportDownloadToken_InvalidToken_ThrowsNotFoundException()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert
        Assert.Throws<NotFoundException>(() => sut.ParseReportDownloadToken("invalid-token"));
    }

    [Fact]
    public async Task GetReportReadStreamAsync_FileExists_ReturnsStream()
    {
        // Arrange
        var fixture = new Fixture();
        var tempDir = Path.Combine(Path.GetTempPath(), "bitwarden-test-" + Guid.NewGuid());

        var globalSettings = new Core.Settings.GlobalSettings();
        globalSettings.OrganizationReport.BaseDirectory = tempDir;
        globalSettings.OrganizationReport.BaseUrl = "https://localhost/reports";
        globalSettings.BaseServiceUri.Api = "https://localhost/api";

        var sut = CreateSut(globalSettings);

        var report = fixture.Build<OrganizationReport>()
            .With(r => r.OrganizationId, Guid.NewGuid())
            .With(r => r.Id, Guid.NewGuid())
            .With(r => r.CreationDate, DateTime.UtcNow)
            .With(r => r.ReportData, string.Empty)
            .Create();

        var fileData = CreateFileData("stream-test-file");
        var testData = "test report content";
        var uploadStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(testData));

        try
        {
            // Upload first
            await sut.UploadReportDataAsync(report, fileData, uploadStream);

            // Act
            var readStream = await sut.GetReportReadStreamAsync(report, fileData);

            // Assert
            Assert.NotNull(readStream);
            using var reader = new StreamReader(readStream);
            var content = await reader.ReadToEndAsync();
            Assert.Equal(testData, content);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task GetReportReadStreamAsync_FileDoesNotExist_ReturnsNull()
    {
        // Arrange
        var fixture = new Fixture();
        var tempDir = Path.Combine(Path.GetTempPath(), "bitwarden-test-" + Guid.NewGuid());

        var globalSettings = new Core.Settings.GlobalSettings();
        globalSettings.OrganizationReport.BaseDirectory = tempDir;
        globalSettings.OrganizationReport.BaseUrl = "https://localhost/reports";
        globalSettings.BaseServiceUri.Api = "https://localhost/api";

        var sut = CreateSut(globalSettings);

        var report = fixture.Build<OrganizationReport>()
            .With(r => r.OrganizationId, Guid.NewGuid())
            .With(r => r.Id, Guid.NewGuid())
            .With(r => r.CreationDate, DateTime.UtcNow)
            .With(r => r.ReportData, string.Empty)
            .Create();

        var fileData = CreateFileData("nonexistent-file");

        // Act
        var stream = await sut.GetReportReadStreamAsync(report, fileData);

        // Assert
        Assert.Null(stream);
    }

    [Theory]
    [InlineData("../../../../etc/malicious")]
    [InlineData("../../../../../tmp/evil")]
    public async Task UploadReportDataAsync_WithPathTraversalPayload_ThrowsInvalidOperationException(string maliciousFileId)
    {
        // Arrange
        var fixture = new Fixture();
        var tempDir = Path.Combine(Path.GetTempPath(), "bitwarden-test-" + Guid.NewGuid());

        var globalSettings = new Core.Settings.GlobalSettings();
        globalSettings.OrganizationReport.BaseDirectory = tempDir;
        globalSettings.OrganizationReport.BaseUrl = "https://localhost/reports";
        globalSettings.BaseServiceUri.Api = "https://localhost/api";

        var sut = CreateSut(globalSettings);

        var report = fixture.Build<OrganizationReport>()
            .With(r => r.OrganizationId, Guid.NewGuid())
            .With(r => r.Id, Guid.NewGuid())
            .With(r => r.CreationDate, DateTime.UtcNow)
            .With(r => r.ReportData, string.Empty)
            .Create();

        var maliciousFileData = new ReportFile
        {
            Id = maliciousFileId,
            FileName = "report-data.json",
            Validated = false
        };

        var testData = "malicious content";
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(testData));

        try
        {
            // Act & Assert - EnsurePathWithinBaseDir guard rejects the traversal attempt
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => sut.UploadReportDataAsync(report, maliciousFileData, stream));
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
        globalSettings.BaseServiceUri.Api = "https://localhost/api";

        var sut = CreateSut(globalSettings);

        var report = fixture.Build<OrganizationReport>()
            .With(r => r.OrganizationId, Guid.NewGuid())
            .With(r => r.Id, Guid.NewGuid())
            .With(r => r.CreationDate, DateTime.UtcNow)
            .With(r => r.ReportData, string.Empty)
            .Create();

        var fileData = CreateFileData("test-file-123");
        var testData = "test report data content";
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(testData));

        try
        {
            // Act
            await sut.UploadReportDataAsync(report, fileData, stream);

            // Assert
            var expectedDir = Path.Combine(tempDir, report.OrganizationId.ToString(),
                report.CreationDate.ToString("MM-dd-yyyy"), report.Id.ToString(), fileData.Id);
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

    [Fact]
    public async Task ValidateFileAsync_FileExists_ReturnsValidAndLength()
    {
        // Arrange
        var fixture = new Fixture();
        var tempDir = Path.Combine(Path.GetTempPath(), "bitwarden-test-" + Guid.NewGuid());

        var globalSettings = new Core.Settings.GlobalSettings();
        globalSettings.OrganizationReport.BaseDirectory = tempDir;
        globalSettings.OrganizationReport.BaseUrl = "https://localhost/reports";
        globalSettings.BaseServiceUri.Api = "https://localhost/api";

        var sut = CreateSut(globalSettings);

        var report = fixture.Build<OrganizationReport>()
            .With(r => r.OrganizationId, Guid.NewGuid())
            .With(r => r.Id, Guid.NewGuid())
            .With(r => r.CreationDate, DateTime.UtcNow)
            .With(r => r.ReportData, string.Empty)
            .Create();

        var fileData = CreateFileData("validate-test-file");
        var testData = "test content for validation";
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(testData));

        try
        {
            // First upload a file
            await sut.UploadReportDataAsync(report, fileData, stream);

            // Act
            var (valid, length) = await sut.ValidateFileAsync(report, fileData, 0, 1000);

            // Assert
            Assert.True(valid);
            Assert.Equal(testData.Length, length);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task ValidateFileAsync_FileDoesNotExist_ReturnsInvalid()
    {
        // Arrange
        var fixture = new Fixture();
        var tempDir = Path.Combine(Path.GetTempPath(), "bitwarden-test-" + Guid.NewGuid());

        var globalSettings = new Core.Settings.GlobalSettings();
        globalSettings.OrganizationReport.BaseDirectory = tempDir;
        globalSettings.OrganizationReport.BaseUrl = "https://localhost/reports";
        globalSettings.BaseServiceUri.Api = "https://localhost/api";

        var sut = CreateSut(globalSettings);

        var report = fixture.Build<OrganizationReport>()
            .With(r => r.OrganizationId, Guid.NewGuid())
            .With(r => r.Id, Guid.NewGuid())
            .With(r => r.CreationDate, DateTime.UtcNow)
            .With(r => r.ReportData, string.Empty)
            .Create();

        var fileData = CreateFileData("nonexistent-file");

        // Act
        var (valid, length) = await sut.ValidateFileAsync(report, fileData, 0, 1000);

        // Assert
        Assert.False(valid);
        Assert.Equal(-1, length);
    }
}
