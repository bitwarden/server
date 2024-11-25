using AutoFixture;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.ReportFeatures;
using Bit.Core.Tools.ReportFeatures.Requests;
using Bit.Core.Tools.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Tools.ReportFeatures;

[SutProviderCustomize]
public class AddPasswordHealthReportApplicationCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task AddPasswordHealthReportApplicationAsync_WithValidRequest_ShouldReturnPasswordHealthReportApplication(
        SutProvider<AddPasswordHealthReportApplicationCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Create<AddPasswordHealthReportApplicationRequest>();
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(fixture.Create<Organization>());

        sutProvider.GetDependency<IPasswordHealthReportApplicationRepository>()
            .CreateAsync(Arg.Any<PasswordHealthReportApplication>())
            .Returns(c => c.Arg<PasswordHealthReportApplication>());

        // Act
        var result = await sutProvider.Sut.AddPasswordHealthReportApplicationAsync(request);

        // Assert
        Assert.NotNull(result);
    }

    [Theory]
    [BitAutoData]
    public async Task AddPasswordHealthReportApplicationAsync_WithInvalidOrganizationId_ShouldThrowError(
        SutProvider<AddPasswordHealthReportApplicationCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Create<AddPasswordHealthReportApplicationRequest>();
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(null as Organization);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.AddPasswordHealthReportApplicationAsync(request));
        Assert.Equal("Invalid Organization", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task AddPasswordHealthReportApplicationAsync_WithInvalidUrl_ShouldThrowError(
        SutProvider<AddPasswordHealthReportApplicationCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Build<AddPasswordHealthReportApplicationRequest>()
                        .Without(_ => _.Url)
                        .Create();

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(fixture.Create<Organization>());

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.AddPasswordHealthReportApplicationAsync(request));
        Assert.Equal("URL is required", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task AddPasswordHealthReportApplicationAsync_Multiples_WithInvalidOrganizationId_ShouldThrowError(
        SutProvider<AddPasswordHealthReportApplicationCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Build<AddPasswordHealthReportApplicationRequest>()
                        .Without(_ => _.OrganizationId)
                        .CreateMany(2).ToList();

        request[0].OrganizationId = Guid.NewGuid();
        request[1].OrganizationId = Guid.Empty;

        // only return an organization for the first request and null for the second
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(Arg.Is<Guid>(x => x == request[0].OrganizationId))
            .Returns(fixture.Create<Organization>());

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.AddPasswordHealthReportApplicationAsync(request));
        Assert.Equal("Invalid Organization", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task AddPasswordHealthReportApplicationAsync_Multiples_WithInvalidUrl_ShouldThrowError(
        SutProvider<AddPasswordHealthReportApplicationCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Build<AddPasswordHealthReportApplicationRequest>()
                        .CreateMany(2).ToList();

        request[1].Url = string.Empty;

        // return an organization for both requests
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(fixture.Create<Organization>());

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.AddPasswordHealthReportApplicationAsync(request));
        Assert.Equal("URL is required", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task AddPasswordHealthReportApplicationAsync_Multiples_WithValidRequest_ShouldBeSuccessful(
    SutProvider<AddPasswordHealthReportApplicationCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.CreateMany<AddPasswordHealthReportApplicationRequest>(2);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(fixture.Create<Organization>());

        sutProvider.GetDependency<IPasswordHealthReportApplicationRepository>()
            .CreateAsync(Arg.Any<PasswordHealthReportApplication>())
            .Returns(c => c.Arg<PasswordHealthReportApplication>());

        // Act
        var result = await sutProvider.Sut.AddPasswordHealthReportApplicationAsync(request);

        // Assert
        Assert.True(result.Count() == 2);
        sutProvider.GetDependency<IOrganizationRepository>().Received(2);
        sutProvider.GetDependency<IPasswordHealthReportApplicationRepository>().Received(2);
    }
}
