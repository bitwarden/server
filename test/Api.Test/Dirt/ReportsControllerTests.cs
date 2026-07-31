using AutoFixture;
using Bit.Api.Dirt.Controllers;
using Bit.Api.Dirt.Models;
using Bit.Core.Context;
using Bit.Core.Dirt.Reports.Models.Data;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;
using Bit.Core.Exceptions;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Dirt;


[ControllerCustomize(typeof(ReportsController))]
[SutProviderCustomize]
public class ReportsControllerTests
{
    [Theory, BitAutoData]
    public async Task GetPasswordHealthReportApplicationAsync_Success(SutProvider<ReportsController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<ICurrentContext>().AccessReports(Arg.Any<Guid>()).Returns(true);

        // Act
        var orgId = Guid.NewGuid();
        var result = await sutProvider.Sut.GetPasswordHealthReportApplications(orgId);

        // Assert
        _ = sutProvider.GetDependency<IGetPasswordHealthReportApplicationQuery>()
            .Received(1)
            .GetPasswordHealthReportApplicationAsync(Arg.Is<Guid>(_ => _ == orgId));
    }

    [Theory, BitAutoData]
    public async Task GetPasswordHealthReportApplicationAsync_withoutAccess(SutProvider<ReportsController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<ICurrentContext>().AccessReports(Arg.Any<Guid>()).Returns(false);

        // Act & Assert
        var orgId = Guid.NewGuid();
        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.GetPasswordHealthReportApplications(orgId));

        // Assert
        _ = sutProvider.GetDependency<IGetPasswordHealthReportApplicationQuery>()
            .Received(0);
    }

    [Theory, BitAutoData]
    public async Task AddPasswordHealthReportApplicationAsync_withAccess_success(SutProvider<ReportsController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<ICurrentContext>().AccessReports(Arg.Any<Guid>()).Returns(true);

        // Act
        var request = new PasswordHealthReportApplicationModel
        {
            OrganizationId = Guid.NewGuid(),
            Url = "https://example.com",
        };
        await sutProvider.Sut.AddPasswordHealthReportApplication(request);

        // Assert
        _ = sutProvider.GetDependency<IAddPasswordHealthReportApplicationCommand>()
            .Received(1)
            .AddPasswordHealthReportApplicationAsync(Arg.Is<AddPasswordHealthReportApplicationRequest>(_ =>
                _.OrganizationId == request.OrganizationId && _.Url == request.Url));
    }

    [Theory, BitAutoData]
    public async Task AddPasswordHealthReportApplicationAsync_multiple_withAccess_success(
        SutProvider<ReportsController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<ICurrentContext>().AccessReports(Arg.Any<Guid>()).Returns(true);

        // Act
        var fixture = new Fixture();
        var request = fixture.CreateMany<PasswordHealthReportApplicationModel>(2);
        await sutProvider.Sut.AddPasswordHealthReportApplications(request);

        // Assert
        _ = sutProvider.GetDependency<IAddPasswordHealthReportApplicationCommand>()
            .Received(1)
            .AddPasswordHealthReportApplicationAsync(Arg.Any<IEnumerable<AddPasswordHealthReportApplicationRequest>>());
    }

    [Theory, BitAutoData]
    public async Task AddPasswordHealthReportApplicationAsync_withoutAccess(SutProvider<ReportsController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<ICurrentContext>().AccessReports(Arg.Any<Guid>()).Returns(false);

        // Act
        var request = new PasswordHealthReportApplicationModel
        {
            OrganizationId = Guid.NewGuid(),
            Url = "https://example.com",
        };
        await Assert.ThrowsAsync<NotFoundException>(async () =>
                await sutProvider.Sut.AddPasswordHealthReportApplication(request));

        // Assert
        _ = sutProvider.GetDependency<IAddPasswordHealthReportApplicationCommand>()
            .Received(0);
    }

    [Theory, BitAutoData]
    public async Task DropPasswordHealthReportApplicationAsync_withoutAccess(SutProvider<ReportsController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<ICurrentContext>().AccessReports(Arg.Any<Guid>()).Returns(false);

        // Act
        var fixture = new Fixture();
        var request = fixture.Create<PasswordHealthReportApplicationModel>();
        await Assert.ThrowsAsync<NotFoundException>(async () =>
                await sutProvider.Sut.AddPasswordHealthReportApplication(request));

        // Assert
        _ = sutProvider.GetDependency<IDropPasswordHealthReportApplicationCommand>()
            .Received(0);
    }

    [Theory, BitAutoData]
    public async Task DropPasswordHealthReportApplicationAsync_withAccess_success(SutProvider<ReportsController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<ICurrentContext>().AccessReports(Arg.Any<Guid>()).Returns(true);

        // Act
        var fixture = new Fixture();
        var request = fixture.Create<DropPasswordHealthReportApplicationRequest>();
        await sutProvider.Sut.DropPasswordHealthReportApplication(request);

        // Assert
        _ = sutProvider.GetDependency<IDropPasswordHealthReportApplicationCommand>()
            .Received(1)
            .DropPasswordHealthReportApplicationAsync(Arg.Is<DropPasswordHealthReportApplicationRequest>(_ =>
                _.OrganizationId == request.OrganizationId &&
                _.PasswordHealthReportApplicationIds == request.PasswordHealthReportApplicationIds));
    }

    [Theory, BitAutoData]
    public async Task GetPasskeyDirectory_ReturnsExpectedEntries(SutProvider<ReportsController> sutProvider)
    {
        // Arrange
        var entries = new List<PasskeyDirectoryEntry>
        {
            new() { DomainName = "example.com", Passwordless = true, Mfa = false, Instructions = "https://example.com/help" },
            new() { DomainName = "test.com", Passwordless = false, Mfa = true, Instructions = "" }
        };
        sutProvider.GetDependency<IGetPasskeyDirectoryQuery>()
            .GetPasskeyDirectoryAsync()
            .Returns(entries);

        // Act
        var result = (await sutProvider.Sut.GetPasskeyDirectoryAsync()).ToList();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("example.com", result[0].DomainName);
        Assert.True(result[0].Passwordless);
        Assert.False(result[0].Mfa);
        Assert.Equal("https://example.com/help", result[0].Instructions);
        Assert.Equal("test.com", result[1].DomainName);
        Assert.False(result[1].Passwordless);
        Assert.True(result[1].Mfa);
    }
}
