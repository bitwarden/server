
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
public class AddOrganizationReportCommandTests
{

    [Theory]
    [BitAutoData]
    public async Task AddOrganizationReportAsync_ShouldReturnOrganizationReport(
        SutProvider<AddOrganizationReportCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Create<AddOrganizationReportRequest>();
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(fixture.Create<Organization>());

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .CreateAsync(Arg.Any<OrganizationReport>())
            .Returns(c => c.Arg<OrganizationReport>());

        // Act
        var result = await sutProvider.Sut.AddOrganizationReportAsync(request);

        // Assert
        Assert.NotNull(result);
    }

    [Theory]
    [BitAutoData]
    public async Task AddOrganizationReportAsync_WithInvalidOrganizationId_ShouldThrowError(
        SutProvider<AddOrganizationReportCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Create<AddOrganizationReportRequest>();
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(null as Organization);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.AddOrganizationReportAsync(request));
        Assert.Equal("Invalid Organization", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task AddOrganizationReportAsync_WithInvalidUrl_ShouldThrowError(
        SutProvider<AddOrganizationReportCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Build<AddOrganizationReportRequest>()
                        .Without(_ => _.ReportData)
                        .Create();

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(fixture.Create<Organization>());

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.AddOrganizationReportAsync(request));
        Assert.Equal("Report Data is required", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task AddOrganizationReportAsync_Multiples_WithInvalidOrganizationId_ShouldThrowError(
        SutProvider<AddOrganizationReportCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Create<AddOrganizationReportRequest>();
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(null as Organization);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.AddOrganizationReportAsync(request));
        Assert.Equal("Invalid Organization", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task AddOrganizationReportAsync_Multiples_WithInvalidUrl_ShouldThrowError(
        SutProvider<AddOrganizationReportCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Build<AddOrganizationReportRequest>()
                        .Without(_ => _.ReportData)
                        .Create();

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(fixture.Create<Organization>());

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.AddOrganizationReportAsync(request));
        Assert.Equal("Report Data is required", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task AddOrganizationReportAsync_WithNullOrganizationId_ShouldThrowError(
        SutProvider<AddOrganizationReportCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Build<AddOrganizationReportRequest>()
            .With(x => x.OrganizationId, default(Guid))
            .Create();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.AddOrganizationReportAsync(request));
        Assert.Equal("Invalid Organization", exception.Message);
    }
}
