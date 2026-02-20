using Bit.Api.Dirt.Controllers;
using Bit.Api.Dirt.Models.Response;
using Bit.Core.Context;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Models.Data;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Dirt;

[ControllerCustomize(typeof(OrganizationReportsV2Controller))]
[SutProviderCustomize]
public class OrganizationReportsV2ControllerTests
{
    private static void SetupAuthorization(SutProvider<OrganizationReportsV2Controller> sutProvider,
        Guid orgId, bool accessReports = true, bool useRiskInsights = true)
    {
        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(accessReports);

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(orgId)
            .Returns(new OrganizationAbility { UseRiskInsights = useRiskInsights });
    }

    #region GetOrganizationReportApplicationDataV2Async

    [Theory, BitAutoData]
    public async Task GetOrganizationReportApplicationDataV2Async_WithValidIds_ReturnsResponse(
        SutProvider<OrganizationReportsV2Controller> sutProvider,
        Guid orgId,
        Guid reportId,
        OrganizationReportApplicationDataResponse expectedApplicationData)
    {
        // Arrange
        SetupAuthorization(sutProvider, orgId);

        sutProvider.GetDependency<IGetOrganizationReportApplicationDataV2Query>()
            .GetApplicationDataAsync(orgId, reportId)
            .Returns(expectedApplicationData);

        // Act
        var result = await sutProvider.Sut.GetOrganizationReportApplicationDataV2Async(orgId, reportId);

        // Assert
        Assert.Equal(expectedApplicationData, result);
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationReportApplicationDataV2Async_WithoutAccess_ThrowsNotFoundException(
        SutProvider<OrganizationReportsV2Controller> sutProvider,
        Guid orgId,
        Guid reportId)
    {
        // Arrange
        SetupAuthorization(sutProvider, orgId, accessReports: false);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetOrganizationReportApplicationDataV2Async(orgId, reportId));

        // Verify that the query was not called
        await sutProvider.GetDependency<IGetOrganizationReportApplicationDataV2Query>()
            .DidNotReceive()
            .GetApplicationDataAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationReportApplicationDataV2Async_UseRiskInsightsDisabled_ThrowsBadRequestException(
        SutProvider<OrganizationReportsV2Controller> sutProvider,
        Guid orgId,
        Guid reportId)
    {
        // Arrange
        SetupAuthorization(sutProvider, orgId, useRiskInsights: false);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.GetOrganizationReportApplicationDataV2Async(orgId, reportId));

        // Verify that the query was not called
        await sutProvider.GetDependency<IGetOrganizationReportApplicationDataV2Query>()
            .DidNotReceive()
            .GetApplicationDataAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationReportApplicationDataV2Async_NullResult_ThrowsNotFoundException(
        SutProvider<OrganizationReportsV2Controller> sutProvider,
        Guid orgId,
        Guid reportId)
    {
        // Arrange
        SetupAuthorization(sutProvider, orgId);

        sutProvider.GetDependency<IGetOrganizationReportApplicationDataV2Query>()
            .GetApplicationDataAsync(orgId, reportId)
            .Returns((OrganizationReportApplicationDataResponse)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetOrganizationReportApplicationDataV2Async(orgId, reportId));

        Assert.Equal("Organization report application data not found.", exception.Message);
    }

    #endregion

    #region UpdateOrganizationReportApplicationDataV2Async

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportApplicationDataV2Async_WithValidRequest_ReturnsResponse(
        SutProvider<OrganizationReportsV2Controller> sutProvider,
        Guid orgId,
        Guid reportId,
        UpdateOrganizationReportApplicationDataRequest request,
        OrganizationReport expectedReport)
    {
        // Arrange
        request.OrganizationId = orgId;
        request.Id = reportId;

        SetupAuthorization(sutProvider, orgId);

        sutProvider.GetDependency<IUpdateOrganizationReportApplicationDataV2Command>()
            .UpdateApplicationDataAsync(request)
            .Returns(expectedReport);

        // Act
        var result = await sutProvider.Sut.UpdateOrganizationReportApplicationDataV2Async(orgId, reportId, request);

        // Assert
        var expectedResponse = new OrganizationReportResponseModel(expectedReport);
        Assert.Equivalent(expectedResponse, result);
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportApplicationDataV2Async_WithoutAccess_ThrowsNotFoundException(
        SutProvider<OrganizationReportsV2Controller> sutProvider,
        Guid orgId,
        Guid reportId,
        UpdateOrganizationReportApplicationDataRequest request)
    {
        // Arrange
        request.OrganizationId = orgId;
        request.Id = reportId;

        SetupAuthorization(sutProvider, orgId, accessReports: false);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.UpdateOrganizationReportApplicationDataV2Async(orgId, reportId, request));

        // Verify that the command was not called
        await sutProvider.GetDependency<IUpdateOrganizationReportApplicationDataV2Command>()
            .DidNotReceive()
            .UpdateApplicationDataAsync(Arg.Any<UpdateOrganizationReportApplicationDataRequest>());
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportApplicationDataV2Async_UseRiskInsightsDisabled_ThrowsBadRequestException(
        SutProvider<OrganizationReportsV2Controller> sutProvider,
        Guid orgId,
        Guid reportId,
        UpdateOrganizationReportApplicationDataRequest request)
    {
        // Arrange
        request.OrganizationId = orgId;
        request.Id = reportId;

        SetupAuthorization(sutProvider, orgId, useRiskInsights: false);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.UpdateOrganizationReportApplicationDataV2Async(orgId, reportId, request));

        // Verify that the command was not called
        await sutProvider.GetDependency<IUpdateOrganizationReportApplicationDataV2Command>()
            .DidNotReceive()
            .UpdateApplicationDataAsync(Arg.Any<UpdateOrganizationReportApplicationDataRequest>());
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportApplicationDataV2Async_MismatchedOrgId_ThrowsBadRequestException(
        SutProvider<OrganizationReportsV2Controller> sutProvider,
        Guid orgId,
        Guid reportId,
        UpdateOrganizationReportApplicationDataRequest request)
    {
        // Arrange
        request.OrganizationId = Guid.NewGuid(); // Different from orgId
        request.Id = reportId;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.UpdateOrganizationReportApplicationDataV2Async(orgId, reportId, request));

        Assert.Equal("Organization ID in the request body must match the route parameter", exception.Message);

        // Verify that the command was not called
        await sutProvider.GetDependency<IUpdateOrganizationReportApplicationDataV2Command>()
            .DidNotReceive()
            .UpdateApplicationDataAsync(Arg.Any<UpdateOrganizationReportApplicationDataRequest>());
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportApplicationDataV2Async_MismatchedReportId_ThrowsBadRequestException(
        SutProvider<OrganizationReportsV2Controller> sutProvider,
        Guid orgId,
        Guid reportId,
        UpdateOrganizationReportApplicationDataRequest request)
    {
        // Arrange
        request.OrganizationId = orgId;
        request.Id = Guid.NewGuid(); // Different from reportId

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.UpdateOrganizationReportApplicationDataV2Async(orgId, reportId, request));

        Assert.Equal("Report ID in the request body must match the route parameter", exception.Message);

        // Verify that the command was not called
        await sutProvider.GetDependency<IUpdateOrganizationReportApplicationDataV2Command>()
            .DidNotReceive()
            .UpdateApplicationDataAsync(Arg.Any<UpdateOrganizationReportApplicationDataRequest>());
    }

    #endregion
}
