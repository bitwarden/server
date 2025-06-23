using AutoFixture;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Reports.ReportFeatures;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Exceptions;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Dirt.ReportFeatures;

[SutProviderCustomize]
public class DeleteOrganizationReportCommandTests
{
    [Theory, BitAutoData]
    public async Task DropOrganizationReportAsync_withValidRequest_Success(
        SutProvider<DropOrganizationReportCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var OrganizationReports = fixture.CreateMany<OrganizationReport>(2).ToList();
        // only take one id from the list - we only want to drop one record
        var request = fixture.Build<DropOrganizationReportRequest>()
                        .With(x => x.OrganizationReportIds,
                            OrganizationReports.Select(x => x.Id).Take(1).ToList())
                        .Create();

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByOrganizationIdAsync(Arg.Any<Guid>())
            .Returns(OrganizationReports);

        // Act
        await sutProvider.Sut.DropOrganizationReportAsync(request);

        // Assert
        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .Received(1)
            .GetByOrganizationIdAsync(request.OrganizationId);

        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .Received(1)
            .DeleteAsync(Arg.Is<OrganizationReport>(_ =>
                request.OrganizationReportIds.Contains(_.Id)));
    }

    [Theory, BitAutoData]
    public async Task DropOrganizationReportAsync_withValidRequest_nothingToDrop(
        SutProvider<DropOrganizationReportCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var OrganizationReports = fixture.CreateMany<OrganizationReport>(2).ToList();
        // we are passing invalid data
        var request = fixture.Build<DropOrganizationReportRequest>()
                .With(x => x.OrganizationReportIds, new List<Guid> { Guid.NewGuid() })
                        .Create();

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByOrganizationIdAsync(Arg.Any<Guid>())
            .Returns(OrganizationReports);

        // Act
        await sutProvider.Sut.DropOrganizationReportAsync(request);

        // Assert
        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .Received(1)
            .GetByOrganizationIdAsync(request.OrganizationId);

        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .Received(0)
            .DeleteAsync(Arg.Any<OrganizationReport>());
    }

    [Theory, BitAutoData]
    public async Task DropOrganizationReportAsync_withNodata_fails(
        SutProvider<DropOrganizationReportCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        // we are passing invalid data
        var request = fixture.Build<DropOrganizationReportRequest>()
                .Create();

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByOrganizationIdAsync(Arg.Any<Guid>())
            .Returns(null as List<OrganizationReport>);

        // Act
        await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.DropOrganizationReportAsync(request));

        // Assert
        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .Received(1)
            .GetByOrganizationIdAsync(request.OrganizationId);

        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .Received(0)
            .DeleteAsync(Arg.Any<OrganizationReport>());
    }

    [Theory, BitAutoData]
    public async Task DropOrganizationReportAsync_withInvalidOrganizationId_ShouldThrowError(
        SutProvider<DropOrganizationReportCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Create<DropOrganizationReportRequest>();
        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByOrganizationIdAsync(Arg.Any<Guid>())
            .Returns(null as List<OrganizationReport>);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.DropOrganizationReportAsync(request));
        Assert.Equal("No data found.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task DropOrganizationReportAsync_withInvalidOrganizationReportId_ShouldThrowError(
        SutProvider<DropOrganizationReportCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Create<DropOrganizationReportRequest>();
        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByOrganizationIdAsync(Arg.Any<Guid>())
            .Returns(new List<OrganizationReport>());

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.DropOrganizationReportAsync(request));
        Assert.Equal("No data found.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task DropOrganizationReportAsync_withNullOrganizationId_ShouldThrowError(
        SutProvider<DropOrganizationReportCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Build<DropOrganizationReportRequest>()
            .With(x => x.OrganizationId, default(Guid))
            .Create();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.DropOrganizationReportAsync(request));
        Assert.Equal("No data found.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task DropOrganizationReportAsync_withNullOrganizationReportIds_ShouldThrowError(
        SutProvider<DropOrganizationReportCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Build<DropOrganizationReportRequest>()
            .With(x => x.OrganizationReportIds, default(List<Guid>))
            .Create();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.DropOrganizationReportAsync(request));
        Assert.Equal("No data found.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task DropOrganizationReportAsync_withEmptyOrganizationReportIds_ShouldThrowError(
        SutProvider<DropOrganizationReportCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Build<DropOrganizationReportRequest>()
            .With(x => x.OrganizationReportIds, new List<Guid>())
            .Create();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.DropOrganizationReportAsync(request));
        Assert.Equal("No data found.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task DropOrganizationReportAsync_withEmptyRequest_ShouldThrowError(
        SutProvider<DropOrganizationReportCommand> sutProvider)
    {
        // Arrange
        var request = new DropOrganizationReportRequest();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.DropOrganizationReportAsync(request));
        Assert.Equal("No data found.", exception.Message);
    }

}
