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
public class CreateOrganizationReportCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task CreateAsync_Success_ReturnsReportWithSerializedFileData(
        SutProvider<CreateOrganizationReportCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Build<AddOrganizationReportRequest>()
            .With(r => r.ContentEncryptionKey, "test-encryption-key")
            .Create();

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(request.OrganizationId)
            .Returns(fixture.Create<Organization>());

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .CreateAsync(Arg.Any<OrganizationReport>())
            .Returns(c => c.Arg<OrganizationReport>());

        // Act
        var report = await sutProvider.Sut.CreateAsync(request);

        // Assert
        Assert.NotNull(report);
        // ReportFile should contain serialized file data
        Assert.NotNull(report.ReportFile);
        var fileData = report.GetReportFile();
        Assert.NotNull(fileData);
        Assert.NotNull(fileData.Id);
        Assert.Equal(32, fileData.Id.Length);
        Assert.Matches("^[a-z0-9]+$", fileData.Id);
        Assert.False(fileData.Validated);

        Assert.Equal(request.SummaryData, report.SummaryData);
        Assert.Equal(request.ApplicationData, report.ApplicationData);

        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .Received(1)
            .CreateAsync(Arg.Is<OrganizationReport>(r =>
                r.OrganizationId == request.OrganizationId &&
                r.SummaryData == request.SummaryData &&
                r.ApplicationData == request.ApplicationData &&
                r.ContentEncryptionKey == "test-encryption-key"));
    }

    [Theory]
    [BitAutoData]
    public async Task CreateAsync_InvalidOrganization_ThrowsBadRequestException(
        SutProvider<CreateOrganizationReportCommand> sutProvider)
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
        SutProvider<CreateOrganizationReportCommand> sutProvider)
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
    public async Task CreateAsync_MissingSummaryData_ThrowsBadRequestException(
        SutProvider<CreateOrganizationReportCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Build<AddOrganizationReportRequest>()
            .With(r => r.ContentEncryptionKey, "test-key")
            .With(r => r.SummaryData, string.Empty)
            .Create();

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(request.OrganizationId)
            .Returns(fixture.Create<Organization>());

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            async () => await sutProvider.Sut.CreateAsync(request));
        Assert.Equal("Summary Data is required", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task CreateAsync_MissingApplicationData_ThrowsBadRequestException(
        SutProvider<CreateOrganizationReportCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Build<AddOrganizationReportRequest>()
            .With(r => r.ContentEncryptionKey, "test-key")
            .With(r => r.SummaryData, "summary")
            .With(r => r.ApplicationData, string.Empty)
            .Create();

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(request.OrganizationId)
            .Returns(fixture.Create<Organization>());

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            async () => await sutProvider.Sut.CreateAsync(request));
        Assert.Equal("Application Data is required", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task CreateAsync_MissingReportMetrics_ThrowsBadRequestException(
        SutProvider<CreateOrganizationReportCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Build<AddOrganizationReportRequest>()
            .With(r => r.ContentEncryptionKey, "test-key")
            .With(r => r.SummaryData, "summary")
            .With(r => r.ApplicationData, "app-data")
            .With(r => r.ReportMetrics, (OrganizationReportMetrics)null)
            .Create();

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(request.OrganizationId)
            .Returns(fixture.Create<Organization>());

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            async () => await sutProvider.Sut.CreateAsync(request));
        Assert.Equal("Report Metrics is required", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task CreateAsync_WithMetrics_StoresMetricsCorrectly(
        SutProvider<CreateOrganizationReportCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var metrics = fixture.Build<OrganizationReportMetrics>()
            .With(m => m.ApplicationCount, 100)
            .With(m => m.MemberCount, 50)
            .Create();

        var request = fixture.Build<AddOrganizationReportRequest>()
            .With(r => r.ContentEncryptionKey, "test-key")
            .With(r => r.ReportMetrics, metrics)
            .Create();

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(request.OrganizationId)
            .Returns(fixture.Create<Organization>());

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .CreateAsync(Arg.Any<OrganizationReport>())
            .Returns(c => c.Arg<OrganizationReport>());

        // Act
        var report = await sutProvider.Sut.CreateAsync(request);

        // Assert
        Assert.Equal(100, report.ApplicationCount);
        Assert.Equal(50, report.MemberCount);
    }
}
