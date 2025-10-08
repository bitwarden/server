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
public class UpdateOrganizationReportApplicationDataCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task UpdateOrganizationReportApplicationDataAsync_WithValidRequest_ShouldReturnUpdatedReport(
        SutProvider<UpdateOrganizationReportApplicationDataCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Build<UpdateOrganizationReportApplicationDataRequest>()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.OrganizationId, Guid.NewGuid())
            .With(x => x.ApplicationData, "updated application data")
            .Create();

        var organization = fixture.Create<Organization>();
        var existingReport = fixture.Build<OrganizationReport>()
            .With(x => x.Id, request.Id)
            .With(x => x.OrganizationId, request.OrganizationId)
            .Create();
        var updatedReport = fixture.Build<OrganizationReport>()
            .With(x => x.Id, request.Id)
            .With(x => x.OrganizationId, request.OrganizationId)
            .Create();

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(request.OrganizationId)
            .Returns(organization);
        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(request.Id)
            .Returns(existingReport);
        sutProvider.GetDependency<IOrganizationReportRepository>()
            .UpdateApplicationDataAsync(request.OrganizationId, request.Id, request.ApplicationData)
            .Returns(updatedReport);

        // Act
        var result = await sutProvider.Sut.UpdateOrganizationReportApplicationDataAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(updatedReport.Id, result.Id);
        Assert.Equal(updatedReport.OrganizationId, result.OrganizationId);
        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .Received(1).UpdateApplicationDataAsync(request.OrganizationId, request.Id, request.ApplicationData);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateOrganizationReportApplicationDataAsync_WithEmptyOrganizationId_ShouldThrowBadRequestException(
        SutProvider<UpdateOrganizationReportApplicationDataCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Build<UpdateOrganizationReportApplicationDataRequest>()
            .With(x => x.OrganizationId, Guid.Empty)
            .Create();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.UpdateOrganizationReportApplicationDataAsync(request));

        Assert.Equal("OrganizationId is required", exception.Message);
        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .DidNotReceive().UpdateApplicationDataAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>());
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateOrganizationReportApplicationDataAsync_WithEmptyId_ShouldThrowBadRequestException(
        SutProvider<UpdateOrganizationReportApplicationDataCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Build<UpdateOrganizationReportApplicationDataRequest>()
            .With(x => x.Id, Guid.Empty)
            .Create();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.UpdateOrganizationReportApplicationDataAsync(request));

        Assert.Equal("Id is required", exception.Message);
        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .DidNotReceive().UpdateApplicationDataAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>());
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateOrganizationReportApplicationDataAsync_WithInvalidOrganization_ShouldThrowBadRequestException(
        SutProvider<UpdateOrganizationReportApplicationDataCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Create<UpdateOrganizationReportApplicationDataRequest>();

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(request.OrganizationId)
            .Returns((Organization)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.UpdateOrganizationReportApplicationDataAsync(request));

        Assert.Equal("Invalid Organization", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateOrganizationReportApplicationDataAsync_WithEmptyApplicationData_ShouldThrowBadRequestException(
        SutProvider<UpdateOrganizationReportApplicationDataCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Build<UpdateOrganizationReportApplicationDataRequest>()
            .With(x => x.ApplicationData, string.Empty)
            .Create();

        var organization = fixture.Create<Organization>();
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(request.OrganizationId)
            .Returns(organization);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.UpdateOrganizationReportApplicationDataAsync(request));

        Assert.Equal("Application Data is required", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateOrganizationReportApplicationDataAsync_WithNullApplicationData_ShouldThrowBadRequestException(
        SutProvider<UpdateOrganizationReportApplicationDataCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Build<UpdateOrganizationReportApplicationDataRequest>()
            .With(x => x.ApplicationData, (string)null)
            .Create();

        var organization = fixture.Create<Organization>();
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(request.OrganizationId)
            .Returns(organization);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.UpdateOrganizationReportApplicationDataAsync(request));

        Assert.Equal("Application Data is required", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateOrganizationReportApplicationDataAsync_WithNonExistentReport_ShouldThrowNotFoundException(
        SutProvider<UpdateOrganizationReportApplicationDataCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Create<UpdateOrganizationReportApplicationDataRequest>();
        var organization = fixture.Create<Organization>();

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(request.OrganizationId)
            .Returns(organization);
        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(request.Id)
            .Returns((OrganizationReport)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(async () =>
            await sutProvider.Sut.UpdateOrganizationReportApplicationDataAsync(request));

        Assert.Equal("Organization report not found", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateOrganizationReportApplicationDataAsync_WithMismatchedOrganizationId_ShouldThrowBadRequestException(
        SutProvider<UpdateOrganizationReportApplicationDataCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Create<UpdateOrganizationReportApplicationDataRequest>();
        var organization = fixture.Create<Organization>();
        var existingReport = fixture.Build<OrganizationReport>()
            .With(x => x.Id, request.Id)
            .With(x => x.OrganizationId, Guid.NewGuid()) // Different org ID
            .Create();

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(request.OrganizationId)
            .Returns(organization);
        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(request.Id)
            .Returns(existingReport);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.UpdateOrganizationReportApplicationDataAsync(request));

        Assert.Equal("Organization report does not belong to the specified organization", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateOrganizationReportApplicationDataAsync_WhenRepositoryThrowsException_ShouldPropagateException(
        SutProvider<UpdateOrganizationReportApplicationDataCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Create<UpdateOrganizationReportApplicationDataRequest>();
        var organization = fixture.Create<Organization>();
        var existingReport = fixture.Build<OrganizationReport>()
            .With(x => x.Id, request.Id)
            .With(x => x.OrganizationId, request.OrganizationId)
            .Create();

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(request.OrganizationId)
            .Returns(organization);
        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(request.Id)
            .Returns(existingReport);
        sutProvider.GetDependency<IOrganizationReportRepository>()
            .UpdateApplicationDataAsync(request.OrganizationId, request.Id, request.ApplicationData)
            .Throws(new InvalidOperationException("Database connection failed"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await sutProvider.Sut.UpdateOrganizationReportApplicationDataAsync(request));

        Assert.Equal("Database connection failed", exception.Message);
    }
}
