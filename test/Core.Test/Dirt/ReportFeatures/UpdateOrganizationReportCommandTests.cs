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
using NSubstitute.ExceptionExtensions;
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
            .Create();

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(request.OrganizationId)
            .Returns(organization);
        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(request.ReportId)
            .Returns(existingReport);
        sutProvider.GetDependency<IOrganizationReportRepository>()
            .ReplaceAsync(Arg.Any<OrganizationReport>())
            .Returns(Task.CompletedTask);
        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetLatestByOrganizationIdAsync(request.ReportId)
            .Returns(updatedReport);

        // Act
        var result = await sutProvider.Sut.UpdateOrganizationReportAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(updatedReport.Id, result.Id);
        Assert.Equal(updatedReport.OrganizationId, result.OrganizationId);
        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .Received(1).ReplaceAsync(Arg.Is<OrganizationReport>(r =>
                r.Id == request.ReportId &&
                r.OrganizationId == request.OrganizationId &&
                r.ReportData == request.ReportData));
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
            .DidNotReceive().ReplaceAsync(Arg.Any<OrganizationReport>());
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
            .DidNotReceive().ReplaceAsync(Arg.Any<OrganizationReport>());
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

    [Theory]
    [BitAutoData]
    public async Task UpdateOrganizationReportAsync_WhenRepositoryThrowsException_ShouldPropagateException(
        SutProvider<UpdateOrganizationReportCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Create<UpdateOrganizationReportRequest>();
        var organization = fixture.Create<Organization>();
        var existingReport = fixture.Build<OrganizationReport>()
            .With(x => x.Id, request.ReportId)
            .With(x => x.OrganizationId, request.OrganizationId)
            .Create();

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(request.OrganizationId)
            .Returns(organization);
        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(request.ReportId)
            .Returns(existingReport);
        sutProvider.GetDependency<IOrganizationReportRepository>()
            .ReplaceAsync(Arg.Any<OrganizationReport>())
            .Throws(new InvalidOperationException("Database connection failed"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await sutProvider.Sut.UpdateOrganizationReportAsync(request));

        Assert.Equal("Database connection failed", exception.Message);
    }
}
