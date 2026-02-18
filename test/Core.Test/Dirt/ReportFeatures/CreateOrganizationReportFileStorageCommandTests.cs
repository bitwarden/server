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
public class CreateOrganizationReportFileStorageCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task CreateAsync_Success_ReturnsReportAndGeneratesFileId(
        SutProvider<CreateOrganizationReportFileStorageCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Build<AddOrganizationReportRequest>()
            .With(r => r.ContentEncryptionKey, "test-encryption-key")
            .With(r => r.FileId, "encrypted-file-id-from-client")
            .Create();

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(request.OrganizationId)
            .Returns(fixture.Create<Organization>());

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .CreateAsync(Arg.Any<OrganizationReport>())
            .Returns(c => c.Arg<OrganizationReport>());

        // Act
        var (report, reportFileId) = await sutProvider.Sut.CreateAsync(request);

        // Assert
        Assert.NotNull(report);
        Assert.NotNull(reportFileId);
        Assert.NotEmpty(reportFileId);
        Assert.Equal(32, reportFileId.Length); // SecureRandomString(32)
        Assert.Matches("^[a-z0-9]+$", reportFileId); // Only lowercase alphanumeric

        // Data fields should be empty for file storage
        Assert.Empty(report.ReportData);
        Assert.Null(report.SummaryData);
        Assert.Null(report.ApplicationData);

        // Encrypted FileId from client should be stored
        Assert.Equal("encrypted-file-id-from-client", report.FileId);

        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .Received(1)
            .CreateAsync(Arg.Is<OrganizationReport>(r =>
                r.OrganizationId == request.OrganizationId &&
                r.ReportData == string.Empty &&
                r.SummaryData == null &&
                r.ApplicationData == null &&
                r.FileId == "encrypted-file-id-from-client" &&
                r.ContentEncryptionKey == "test-encryption-key"));
    }

    [Theory]
    [BitAutoData]
    public async Task CreateAsync_InvalidOrganization_ThrowsBadRequestException(
        SutProvider<CreateOrganizationReportFileStorageCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Build<AddOrganizationReportRequest>()
            .With(r => r.ContentEncryptionKey, "test-key")
            .Create();

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(request.OrganizationId)
            .Returns(null as Organization);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            async () => await sutProvider.Sut.CreateAsync(request));
        Assert.Equal("Invalid Organization", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task CreateAsync_MissingContentEncryptionKey_ThrowsBadRequestException(
        SutProvider<CreateOrganizationReportFileStorageCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Build<AddOrganizationReportRequest>()
            .With(r => r.ContentEncryptionKey, string.Empty)
            .Create();

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(request.OrganizationId)
            .Returns(fixture.Create<Organization>());

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            async () => await sutProvider.Sut.CreateAsync(request));
        Assert.Equal("Content Encryption Key is required", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task CreateAsync_WithMetrics_StoresMetricsCorrectly(
        SutProvider<CreateOrganizationReportFileStorageCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var metrics = fixture.Build<OrganizationReportMetricsRequest>()
            .With(m => m.ApplicationCount, 100)
            .With(m => m.MemberCount, 50)
            .Create();

        var request = fixture.Build<AddOrganizationReportRequest>()
            .With(r => r.ContentEncryptionKey, "test-key")
            .With(r => r.Metrics, metrics)
            .Create();

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(request.OrganizationId)
            .Returns(fixture.Create<Organization>());

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .CreateAsync(Arg.Any<OrganizationReport>())
            .Returns(c => c.Arg<OrganizationReport>());

        // Act
        var (report, _) = await sutProvider.Sut.CreateAsync(request);

        // Assert
        Assert.Equal(100, report.ApplicationCount);
        Assert.Equal(50, report.MemberCount);
    }
}
