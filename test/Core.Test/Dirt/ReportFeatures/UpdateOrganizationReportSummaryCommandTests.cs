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
public class UpdateOrganizationReportSummaryCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task UpdateOrganizationReportSummaryAsync_WithValidRequest_ShouldReturnUpdatedReport(
        SutProvider<UpdateOrganizationReportSummaryCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Build<UpdateOrganizationReportSummaryRequest>()
            .With(x => x.ReportId, Guid.NewGuid())
            .With(x => x.OrganizationId, Guid.NewGuid())
            .With(x => x.SummaryData, "updated summary data")
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
            .UpdateSummaryDataAsync(request.ReportId, request.SummaryData)
            .Returns(updatedReport);

        // Act
        var result = await sutProvider.Sut.UpdateOrganizationReportSummaryAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(updatedReport.Id, result.Id);
        Assert.Equal(updatedReport.OrganizationId, result.OrganizationId);
        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .Received(1).UpdateSummaryDataAsync(request.ReportId, request.SummaryData);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateOrganizationReportSummaryAsync_WithEmptyOrganizationId_ShouldThrowBadRequestException(
        SutProvider<UpdateOrganizationReportSummaryCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Build<UpdateOrganizationReportSummaryRequest>()
            .With(x => x.OrganizationId, Guid.Empty)
            .Create();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.UpdateOrganizationReportSummaryAsync(request));

        Assert.Equal("OrganizationId is required", exception.Message);
        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .DidNotReceive().UpdateSummaryDataAsync(Arg.Any<Guid>(), Arg.Any<string>());
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateOrganizationReportSummaryAsync_WithEmptyReportId_ShouldThrowBadRequestException(
        SutProvider<UpdateOrganizationReportSummaryCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Build<UpdateOrganizationReportSummaryRequest>()
            .With(x => x.ReportId, Guid.Empty)
            .Create();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.UpdateOrganizationReportSummaryAsync(request));

        Assert.Equal("ReportId is required", exception.Message);
        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .DidNotReceive().UpdateSummaryDataAsync(Arg.Any<Guid>(), Arg.Any<string>());
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateOrganizationReportSummaryAsync_WithInvalidOrganization_ShouldThrowBadRequestException(
        SutProvider<UpdateOrganizationReportSummaryCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Create<UpdateOrganizationReportSummaryRequest>();

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(request.OrganizationId)
            .Returns((Organization)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.UpdateOrganizationReportSummaryAsync(request));

        Assert.Equal("Invalid Organization", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateOrganizationReportSummaryAsync_WithEmptySummaryData_ShouldThrowBadRequestException(
        SutProvider<UpdateOrganizationReportSummaryCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Build<UpdateOrganizationReportSummaryRequest>()
            .With(x => x.SummaryData, string.Empty)
            .Create();

        var organization = fixture.Create<Organization>();
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(request.OrganizationId)
            .Returns(organization);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.UpdateOrganizationReportSummaryAsync(request));

        Assert.Equal("Summary Data is required", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateOrganizationReportSummaryAsync_WithNullSummaryData_ShouldThrowBadRequestException(
        SutProvider<UpdateOrganizationReportSummaryCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Build<UpdateOrganizationReportSummaryRequest>()
            .With(x => x.SummaryData, (string)null)
            .Create();

        var organization = fixture.Create<Organization>();
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(request.OrganizationId)
            .Returns(organization);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.UpdateOrganizationReportSummaryAsync(request));

        Assert.Equal("Summary Data is required", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateOrganizationReportSummaryAsync_WithNonExistentReport_ShouldThrowNotFoundException(
        SutProvider<UpdateOrganizationReportSummaryCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Create<UpdateOrganizationReportSummaryRequest>();
        var organization = fixture.Create<Organization>();

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(request.OrganizationId)
            .Returns(organization);
        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(request.ReportId)
            .Returns((OrganizationReport)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(async () =>
            await sutProvider.Sut.UpdateOrganizationReportSummaryAsync(request));

        Assert.Equal("Organization report not found", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateOrganizationReportSummaryAsync_WithMismatchedOrganizationId_ShouldThrowBadRequestException(
        SutProvider<UpdateOrganizationReportSummaryCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Create<UpdateOrganizationReportSummaryRequest>();
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
            await sutProvider.Sut.UpdateOrganizationReportSummaryAsync(request));

        Assert.Equal("Organization report does not belong to the specified organization", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateOrganizationReportSummaryAsync_WhenRepositoryThrowsException_ShouldPropagateException(
        SutProvider<UpdateOrganizationReportSummaryCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Create<UpdateOrganizationReportSummaryRequest>();
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
            .UpdateSummaryDataAsync(request.ReportId, request.SummaryData)
            .Throws(new InvalidOperationException("Database connection failed"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await sutProvider.Sut.UpdateOrganizationReportSummaryAsync(request));

        Assert.Equal("Database connection failed", exception.Message);
    }
}
