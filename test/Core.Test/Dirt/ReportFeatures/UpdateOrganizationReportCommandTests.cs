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
public class UpdateOrganizationReportCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task UpdateOrganizationReportAsync_WithValidRequest_ShouldReturnUpdatedReport(
        SutProvider<UpdateOrganizationReportCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Build<UpdateOrganizationReportRequest>()
            .With(x => x.ReportId, Guid.NewGuid())
            .With(x => x.OrganizationId, Guid.NewGuid())
            .With(x => x.ReportData, "updated report data")
            .Create();

        var organization = fixture.Create<Organization>();
        var existingReport = fixture.Build<OrganizationReport>()
            .With(x => x.Id, request.ReportId)
            .With(x => x.OrganizationId, request.OrganizationId)
            .Create();
        var updatedReport = fixture.Build<OrganizationReport>()
            .With(x => x.Id, request.ReportId)
            .With(x => x.OrganizationId, request.OrganizationId)
            .With(x => x.ReportData, request.ReportData)
            .Create();

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(request.OrganizationId)
            .Returns(organization);
        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(request.ReportId)
            .Returns(existingReport);
        sutProvider.GetDependency<IOrganizationReportRepository>()
            .UpsertAsync(Arg.Any<OrganizationReport>())
            .Returns(Task.CompletedTask);
        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(request.ReportId)
            .Returns(existingReport, updatedReport);

        // Act
        var result = await sutProvider.Sut.UpdateOrganizationReportAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(updatedReport.Id, result.Id);
        Assert.Equal(updatedReport.OrganizationId, result.OrganizationId);
        Assert.Equal(updatedReport.ReportData, result.ReportData);

        await sutProvider.GetDependency<IOrganizationRepository>()
            .Received(1).GetByIdAsync(request.OrganizationId);
        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .Received(2).GetByIdAsync(request.ReportId);
        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .Received(1).UpsertAsync(Arg.Any<OrganizationReport>());
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateOrganizationReportAsync_WithEmptyOrganizationId_ShouldThrowBadRequestException(
        SutProvider<UpdateOrganizationReportCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Build<UpdateOrganizationReportRequest>()
            .With(x => x.OrganizationId, Guid.Empty)
            .Create();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.UpdateOrganizationReportAsync(request));

        Assert.Equal("OrganizationId is required", exception.Message);
        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .DidNotReceive().UpsertAsync(Arg.Any<OrganizationReport>());
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateOrganizationReportAsync_WithEmptyReportId_ShouldThrowBadRequestException(
        SutProvider<UpdateOrganizationReportCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Build<UpdateOrganizationReportRequest>()
            .With(x => x.ReportId, Guid.Empty)
            .Create();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.UpdateOrganizationReportAsync(request));

        Assert.Equal("ReportId is required", exception.Message);
        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .DidNotReceive().UpsertAsync(Arg.Any<OrganizationReport>());
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateOrganizationReportAsync_WithInvalidOrganization_ShouldThrowBadRequestException(
        SutProvider<UpdateOrganizationReportCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Create<UpdateOrganizationReportRequest>();

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(request.OrganizationId)
            .Returns((Organization)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.UpdateOrganizationReportAsync(request));

        Assert.Equal("Invalid Organization", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateOrganizationReportAsync_WithEmptyReportData_ShouldThrowBadRequestException(
        SutProvider<UpdateOrganizationReportCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Build<UpdateOrganizationReportRequest>()
            .With(x => x.ReportData, string.Empty)
            .Create();

        var organization = fixture.Create<Organization>();
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(request.OrganizationId)
            .Returns(organization);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.UpdateOrganizationReportAsync(request));

        Assert.Equal("Report Data is required", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateOrganizationReportAsync_WithNullReportData_ShouldThrowBadRequestException(
        SutProvider<UpdateOrganizationReportCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Build<UpdateOrganizationReportRequest>()
            .With(x => x.ReportData, (string)null)
            .Create();

        var organization = fixture.Create<Organization>();
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(request.OrganizationId)
            .Returns(organization);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.UpdateOrganizationReportAsync(request));

        Assert.Equal("Report Data is required", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateOrganizationReportAsync_WithNonExistentReport_ShouldThrowNotFoundException(
        SutProvider<UpdateOrganizationReportCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Create<UpdateOrganizationReportRequest>();
        var organization = fixture.Create<Organization>();

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(request.OrganizationId)
            .Returns(organization);
        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(request.ReportId)
            .Returns((OrganizationReport)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(async () =>
            await sutProvider.Sut.UpdateOrganizationReportAsync(request));

        Assert.Equal("Organization report not found", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateOrganizationReportAsync_WithMismatchedOrganizationId_ShouldThrowBadRequestException(
        SutProvider<UpdateOrganizationReportCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Create<UpdateOrganizationReportRequest>();
        var organization = fixture.Create<Organization>();
        var existingReport = fixture.Build<OrganizationReport>()
            .With(x => x.Id, request.ReportId)
            .With(x => x.OrganizationId, Guid.NewGuid()) // Different org ID
            .Create();

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(request.OrganizationId)
            .Returns(organization);
        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(request.ReportId)
            .Returns(existingReport);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.UpdateOrganizationReportAsync(request));

        Assert.Equal("Organization report does not belong to the specified organization", exception.Message);
    }
}
