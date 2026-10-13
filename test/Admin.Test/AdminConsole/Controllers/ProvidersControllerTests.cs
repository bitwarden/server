using Bit.Admin.AdminConsole.Controllers;
using Bit.Admin.AdminConsole.Models;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Providers.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using NSubstitute.ReceivedExtensions;
using Stripe;

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

    #region CreateBusinessUnitAsync
    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task CreateBusinessUnitAsync_WithValidModel_CreatesProvider(
        CreateBusinessUnitProviderModel model,
        SutProvider<ProvidersController> sutProvider)
    {
        // Arrange

        // Act
        var actual = await sutProvider.Sut.CreateBusinessUnit(model);

        // Assert
        Assert.NotNull(actual);
        await sutProvider.GetDependency<ICreateProviderCommand>()
            .Received(Quantity.Exactly(1))
            .CreateBusinessUnitAsync(
                Arg.Is<Provider>(x => x.Type == ProviderType.BusinessUnit),
                model.OwnerEmail,
                Arg.Is<PlanType>(y => y == model.Plan),
                model.EnterpriseSeatMinimum);
    }

    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task CreateBusinessUnitAsync_RedirectsToExpectedPage_AfterCreatingProvider(
        CreateBusinessUnitProviderModel model,
        Guid expectedProviderId,
        SutProvider<ProvidersController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<ICreateProviderCommand>()
            .When(x =>
                x.CreateBusinessUnitAsync(
                    Arg.Is<Provider>(y => y.Type == ProviderType.BusinessUnit),
                    model.OwnerEmail,
                    Arg.Is<PlanType>(y => y == model.Plan),
                    model.EnterpriseSeatMinimum))
            .Do(callInfo =>
            {
                var providerArgument = callInfo.ArgAt<Provider>(0);
                providerArgument.Id = expectedProviderId;
            });

        // Act
        var actual = await sutProvider.Sut.CreateBusinessUnit(model);

        // Assert
        Assert.NotNull(actual);
        Assert.IsType<RedirectToActionResult>(actual);
        var actualResult = (RedirectToActionResult)actual;
        Assert.Equal("Edit", actualResult.ActionName);
        Assert.Null(actualResult.ControllerName);
        Assert.Equal(expectedProviderId, actualResult.RouteValues["Id"]);
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

    #region Edit (GET)
    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task Edit_Get_DeletedStripeCustomer_StillRendersPageWithWarning(
        Provider provider,
        SutProvider<ProvidersController> sutProvider)
    {
        // PM-40292: a deleted Stripe customer is returned as a stub (Deleted = true) with null
        // Metadata. The page must still render, PayByInvoice must default to false rather than
        // NRE'ing, and the admin must be warned so they can fix the Gateway Customer ID.
        provider.Type = ProviderType.Msp;
        provider.Status = ProviderStatusType.Billable;

        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(provider.Id).Returns(provider);
        sutProvider.GetDependency<ISubscriberService>()
            .GetCustomer(provider)
            .Returns(new Customer { Deleted = true, Metadata = null });

        sutProvider.Sut.TempData =
            new TempDataDictionary(new DefaultHttpContext(), Substitute.For<ITempDataProvider>());

        var result = await sutProvider.Sut.Edit(provider.Id);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ProviderEditModel>(view.Model);
        Assert.False(model.PayByInvoice);
        Assert.True(sutProvider.Sut.TempData.ContainsKey("Warning"));
        Assert.Equal(
            "Billing information could not be fully loaded. The Stripe customer may have been deleted. " +
            "You can still edit the provider and set a valid Gateway Customer ID.",
            (string)sutProvider.Sut.TempData["Warning"]);
    }
    #endregion
}
