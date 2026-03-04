using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Models.Data;
using Bit.Core.Dirt.Reports.ReportFeatures;
using Bit.Core.Dirt.Reports.Services;
using Bit.Core.Dirt.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Dirt.ReportFeatures;

[SutProviderCustomize]
public class ValidateOrganizationReportFileCommandTests
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
            OrganizationId = organizationId,
            RevisionDate = DateTime.UtcNow.AddDays(-1)
        };
        report.SetReportFileData(fileData);
        return report;
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_ValidFile_SetsValidatedAndUpdatesReport(
        SutProvider<ValidateOrganizationReportFileCommand> sutProvider)
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var fileId = "test-file-id-123";
        var report = CreateReportWithFileData(reportId, organizationId, fileId);
        var originalRevisionDate = report.RevisionDate;

        sutProvider.GetDependency<IOrganizationReportStorageService>()
            .ValidateFileAsync(report, Arg.Any<ReportFile>(), 0, Core.Constants.FileSize501mb)
            .Returns((true, 12345L));

        // Act
        var result = await sutProvider.Sut.ValidateAsync(report, fileId);

        // Assert
        Assert.True(result);

        var fileData = report.GetReportFileData();
        Assert.NotNull(fileData);
        Assert.True(fileData!.Validated);
        Assert.Equal(12345L, fileData.Size);
        Assert.True(report.RevisionDate > originalRevisionDate);

        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .Received(1)
            .ReplaceAsync(report);

        await sutProvider.GetDependency<IOrganizationReportStorageService>()
            .DidNotReceive()
            .DeleteReportFilesAsync(Arg.Any<OrganizationReport>(), Arg.Any<string>());

        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .DidNotReceive()
            .DeleteAsync(Arg.Any<OrganizationReport>());
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_InvalidFile_DeletesBlobAndReport(
        SutProvider<ValidateOrganizationReportFileCommand> sutProvider)
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var fileId = "test-file-id-456";
        var report = CreateReportWithFileData(reportId, organizationId, fileId);

        sutProvider.GetDependency<IOrganizationReportStorageService>()
            .ValidateFileAsync(report, Arg.Any<ReportFile>(), 0, Core.Constants.FileSize501mb)
            .Returns((false, -1L));

        // Act
        var result = await sutProvider.Sut.ValidateAsync(report, fileId);

        // Assert
        Assert.False(result);

        await sutProvider.GetDependency<IOrganizationReportStorageService>()
            .Received(1)
            .DeleteReportFilesAsync(report, fileId);

        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .Received(1)
            .DeleteAsync(report);

        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .DidNotReceive()
            .ReplaceAsync(Arg.Any<OrganizationReport>());
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_NullFileData_ReturnsFalse(
        SutProvider<ValidateOrganizationReportFileCommand> sutProvider)
    {
        // Arrange
        var report = new OrganizationReport
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            ReportData = string.Empty
        };

        // Act
        var result = await sutProvider.Sut.ValidateAsync(report, "any-file-id");

        // Assert
        Assert.False(result);

        await sutProvider.GetDependency<IOrganizationReportStorageService>()
            .DidNotReceive()
            .ValidateFileAsync(Arg.Any<OrganizationReport>(), Arg.Any<ReportFile>(), Arg.Any<long>(), Arg.Any<long>());
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_MismatchedFileId_ReturnsFalse(
        SutProvider<ValidateOrganizationReportFileCommand> sutProvider)
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var report = CreateReportWithFileData(reportId, organizationId, "stored-file-id");

        // Act
        var result = await sutProvider.Sut.ValidateAsync(report, "different-file-id");

        // Assert
        Assert.False(result);

        await sutProvider.GetDependency<IOrganizationReportStorageService>()
            .DidNotReceive()
            .ValidateFileAsync(Arg.Any<OrganizationReport>(), Arg.Any<ReportFile>(), Arg.Any<long>(), Arg.Any<long>());
    }
}
