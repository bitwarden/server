using Bit.Admin.AdminConsole.Controllers;
using Bit.Admin.AdminConsole.Models;
using Bit.Core;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Providers.Interfaces;
using Bit.Core.Billing.Enums;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using NSubstitute.ReceivedExtensions;

namespace Admin.Test.AdminConsole.Controllers;

[ControllerCustomize(typeof(ProvidersController))]
[SutProviderCustomize]
public class ProvidersControllerTests
{
    #region CreateMspAsync
    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task CreateMspAsync_WithValidModel_CreatesProvider(
        CreateMspProviderModel model,
        SutProvider<ProvidersController> sutProvider)
    {
        // Arrange

        // Act
        var actual = await sutProvider.Sut.CreateMsp(model);

        // Assert
        Assert.NotNull(actual);
        await sutProvider.GetDependency<ICreateProviderCommand>()
            .Received(Quantity.Exactly(1))
            .CreateMspAsync(
                Arg.Is<Provider>(x => x.Type == ProviderType.Msp),
                model.OwnerEmail,
                model.TeamsMonthlySeatMinimum,
                model.EnterpriseMonthlySeatMinimum);
    }

    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task CreateMspAsync_RedirectsToExpectedPage_AfterCreatingProvider(
        CreateMspProviderModel model,
        Guid expectedProviderId,
        SutProvider<ProvidersController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<ICreateProviderCommand>()
            .When(x =>
                x.CreateMspAsync(
                    Arg.Is<Provider>(y => y.Type == ProviderType.Msp),
                    model.OwnerEmail,
                    model.TeamsMonthlySeatMinimum,
                    model.EnterpriseMonthlySeatMinimum))
            .Do(callInfo =>
            {
                var providerArgument = callInfo.ArgAt<Provider>(0);
                providerArgument.Id = expectedProviderId;
            });

        // Act
        var actual = await sutProvider.Sut.CreateMsp(model);

        // Assert
        Assert.NotNull(actual);
        Assert.IsType<RedirectToActionResult>(actual);
        var actualResult = (RedirectToActionResult)actual;
        Assert.Equal("Edit", actualResult.ActionName);
        Assert.Null(actualResult.ControllerName);
        Assert.Equal(expectedProviderId, actualResult.RouteValues["Id"]);
    }
    #endregion

    #region CreateMultiOrganizationEnterpriseAsync
    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task CreateMultiOrganizationEnterpriseAsync_WithValidModel_CreatesProvider(
        CreateMultiOrganizationEnterpriseProviderModel model,
        SutProvider<ProvidersController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM12275_MultiOrganizationEnterprises)
            .Returns(true);

        // Act
        var actual = await sutProvider.Sut.CreateMultiOrganizationEnterprise(model);

        // Assert
        Assert.NotNull(actual);
        await sutProvider.GetDependency<ICreateProviderCommand>()
            .Received(Quantity.Exactly(1))
            .CreateMultiOrganizationEnterpriseAsync(
                Arg.Is<Provider>(x => x.Type == ProviderType.MultiOrganizationEnterprise),
                model.OwnerEmail,
                Arg.Is<PlanType>(y => y == model.Plan),
                model.EnterpriseSeatMinimum);
        sutProvider.GetDependency<IFeatureService>()
            .Received(Quantity.Exactly(1))
            .IsEnabled(FeatureFlagKeys.PM12275_MultiOrganizationEnterprises);
    }

    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task CreateMultiOrganizationEnterpriseAsync_RedirectsToExpectedPage_AfterCreatingProvider(
        CreateMultiOrganizationEnterpriseProviderModel model,
        Guid expectedProviderId,
        SutProvider<ProvidersController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<ICreateProviderCommand>()
            .When(x =>
                x.CreateMultiOrganizationEnterpriseAsync(
                    Arg.Is<Provider>(y => y.Type == ProviderType.MultiOrganizationEnterprise),
                    model.OwnerEmail,
                    Arg.Is<PlanType>(y => y == model.Plan),
                    model.EnterpriseSeatMinimum))
            .Do(callInfo =>
            {
                var providerArgument = callInfo.ArgAt<Provider>(0);
                providerArgument.Id = expectedProviderId;
            });

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM12275_MultiOrganizationEnterprises)
            .Returns(true);

        // Act
        var actual = await sutProvider.Sut.CreateMultiOrganizationEnterprise(model);

        // Assert
        Assert.NotNull(actual);
        Assert.IsType<RedirectToActionResult>(actual);
        var actualResult = (RedirectToActionResult)actual;
        Assert.Equal("Edit", actualResult.ActionName);
        Assert.Null(actualResult.ControllerName);
        Assert.Equal(expectedProviderId, actualResult.RouteValues["Id"]);
    }

    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task CreateMultiOrganizationEnterpriseAsync_ChecksFeatureFlag(
        CreateMultiOrganizationEnterpriseProviderModel model,
        SutProvider<ProvidersController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM12275_MultiOrganizationEnterprises)
            .Returns(true);

        // Act
        await sutProvider.Sut.CreateMultiOrganizationEnterprise(model);

        // Assert
        sutProvider.GetDependency<IFeatureService>()
            .Received(Quantity.Exactly(1))
            .IsEnabled(FeatureFlagKeys.PM12275_MultiOrganizationEnterprises);
    }

    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task CreateMultiOrganizationEnterpriseAsync_RedirectsToProviderTypeSelectionPage_WhenFeatureFlagIsDisabled(
        CreateMultiOrganizationEnterpriseProviderModel model,
        SutProvider<ProvidersController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM12275_MultiOrganizationEnterprises)
            .Returns(false);

        // Act
        var actual = await sutProvider.Sut.CreateMultiOrganizationEnterprise(model);

        // Assert
        sutProvider.GetDependency<IFeatureService>()
            .Received(Quantity.Exactly(1))
            .IsEnabled(FeatureFlagKeys.PM12275_MultiOrganizationEnterprises);

        Assert.IsType<RedirectToActionResult>(actual);
        var actualResult = (RedirectToActionResult)actual;
        Assert.Equal("Create", actualResult.ActionName);
        Assert.Null(actualResult.ControllerName);
    }
    #endregion

    #region CreateResellerAsync
    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task CreateResellerAsync_WithValidModel_CreatesProvider(
        CreateResellerProviderModel model,
        SutProvider<ProvidersController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM12275_MultiOrganizationEnterprises)
            .Returns(true);

        // Act
        var actual = await sutProvider.Sut.CreateReseller(model);

        // Assert
        Assert.NotNull(actual);
        await sutProvider.GetDependency<ICreateProviderCommand>()
            .Received(Quantity.Exactly(1))
            .CreateResellerAsync(
                Arg.Is<Provider>(x => x.Type == ProviderType.Reseller));
    }

    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task CreateResellerAsync_RedirectsToExpectedPage_AfterCreatingProvider(
        CreateResellerProviderModel model,
        Guid expectedProviderId,
        SutProvider<ProvidersController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<ICreateProviderCommand>()
            .When(x =>
                x.CreateResellerAsync(
                    Arg.Is<Provider>(y => y.Type == ProviderType.Reseller)))
            .Do(callInfo =>
            {
                var providerArgument = callInfo.ArgAt<Provider>(0);
                providerArgument.Id = expectedProviderId;
            });

        // Act
        var actual = await sutProvider.Sut.CreateReseller(model);

        // Assert
        Assert.NotNull(actual);
        Assert.IsType<RedirectToActionResult>(actual);
        var actualResult = (RedirectToActionResult)actual;
        Assert.Equal("Edit", actualResult.ActionName);
        Assert.Null(actualResult.ControllerName);
        Assert.Equal(expectedProviderId, actualResult.RouteValues["Id"]);
    }
    #endregion
}
