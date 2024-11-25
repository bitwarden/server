using AutoFixture;
using Bit.Api.Tools.Controllers;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Tools.ReportFeatures.Interfaces;
using Bit.Core.Tools.ReportFeatures.Requests;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Tools.Controllers;


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
        var request = new Api.Tools.Models.PasswordHealthReportApplicationModel
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
        var request = fixture.CreateMany<Api.Tools.Models.PasswordHealthReportApplicationModel>(2);
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
        var request = new Api.Tools.Models.PasswordHealthReportApplicationModel
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
        var request = fixture.Create<Api.Tools.Models.PasswordHealthReportApplicationModel>();
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
}
