﻿using AutoFixture;
using Bit.Api.Dirt.Controllers;
using Bit.Api.Dirt.Models;
using Bit.Core.Context;
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
    public async Task AddOrganizationReportAsync_withAccess_success(SutProvider<ReportsController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<ICurrentContext>().AccessReports(Arg.Any<Guid>()).Returns(true);

        // Act
        var request = new AddOrganizationReportRequest
        {
            OrganizationId = Guid.NewGuid(),
            ReportData = "Report Data",
            Date = DateTime.UtcNow
        };
        await sutProvider.Sut.AddOrganizationReport(request);

        // Assert
        _ = sutProvider.GetDependency<IAddOrganizationReportCommand>()
            .Received(1)
            .AddOrganizationReportAsync(Arg.Is<AddOrganizationReportRequest>(_ =>
                _.OrganizationId == request.OrganizationId &&
                _.ReportData == request.ReportData &&
                _.Date == request.Date));
    }

    [Theory, BitAutoData]
    public async Task AddOrganizationReportAsync_withoutAccess(SutProvider<ReportsController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<ICurrentContext>().AccessReports(Arg.Any<Guid>()).Returns(false);
        // Act
        var request = new AddOrganizationReportRequest
        {
            OrganizationId = Guid.NewGuid(),
            ReportData = "Report Data",
            Date = DateTime.UtcNow
        };
        await Assert.ThrowsAsync<NotFoundException>(async () =>
            await sutProvider.Sut.AddOrganizationReport(request));
        // Assert
        _ = sutProvider.GetDependency<IAddOrganizationReportCommand>()
            .Received(0);
    }

    [Theory, BitAutoData]
    public async Task DropOrganizationReportAsync_withAccess_success(SutProvider<ReportsController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<ICurrentContext>().AccessReports(Arg.Any<Guid>()).Returns(true);
        // Act
        var request = new DropOrganizationReportRequest
        {
            OrganizationId = Guid.NewGuid(),
            OrganizationReportIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() }
        };
        await sutProvider.Sut.DropOrganizationReport(request);
        // Assert
        _ = sutProvider.GetDependency<IDropOrganizationReportCommand>()
            .Received(1)
            .DropOrganizationReportAsync(Arg.Is<DropOrganizationReportRequest>(_ =>
                _.OrganizationId == request.OrganizationId &&
                _.OrganizationReportIds.SequenceEqual(request.OrganizationReportIds)));
    }
    [Theory, BitAutoData]
    public async Task DropOrganizationReportAsync_withoutAccess(SutProvider<ReportsController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<ICurrentContext>().AccessReports(Arg.Any<Guid>()).Returns(false);
        // Act
        var request = new DropOrganizationReportRequest
        {
            OrganizationId = Guid.NewGuid(),
            OrganizationReportIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() }
        };
        await Assert.ThrowsAsync<NotFoundException>(async () =>
            await sutProvider.Sut.DropOrganizationReport(request));
        // Assert
        _ = sutProvider.GetDependency<IDropOrganizationReportCommand>()
            .Received(0);
    }
    [Theory, BitAutoData]
    public async Task GetOrganizationReportAsync_withAccess_success(SutProvider<ReportsController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<ICurrentContext>().AccessReports(Arg.Any<Guid>()).Returns(true);
        // Act
        var orgId = Guid.NewGuid();
        var result = await sutProvider.Sut.GetOrganizationReports(orgId);
        // Assert
        _ = sutProvider.GetDependency<IGetOrganizationReportQuery>()
            .Received(1)
            .GetOrganizationReportAsync(Arg.Is<Guid>(_ => _ == orgId));
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationReportAsync_withoutAccess(SutProvider<ReportsController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<ICurrentContext>().AccessReports(Arg.Any<Guid>()).Returns(false);
        // Act
        var orgId = Guid.NewGuid();
        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.GetOrganizationReports(orgId));
        // Assert
        _ = sutProvider.GetDependency<IGetOrganizationReportQuery>()
            .Received(0);

    }

    [Theory, BitAutoData]
    public async Task GetLastestOrganizationReportAsync_withAccess_success(SutProvider<ReportsController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<ICurrentContext>().AccessReports(Arg.Any<Guid>()).Returns(true);

        // Act
        var orgId = Guid.NewGuid();
        var result = await sutProvider.Sut.GetLatestOrganizationReport(orgId);

        // Assert
        _ = sutProvider.GetDependency<IGetOrganizationReportQuery>()
            .Received(1)
            .GetLatestOrganizationReportAsync(Arg.Is<Guid>(_ => _ == orgId));
    }

    [Theory, BitAutoData]
    public async Task GetLastestOrganizationReportAsync_withoutAccess(SutProvider<ReportsController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<ICurrentContext>().AccessReports(Arg.Any<Guid>()).Returns(false);

        // Act
        var orgId = Guid.NewGuid();
        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.GetLatestOrganizationReport(orgId));

        // Assert
        _ = sutProvider.GetDependency<IGetOrganizationReportQuery>()
            .Received(0);
    }
}
