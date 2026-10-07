using Bit.Admin.Controllers;
using Bit.Admin.Models;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Vault.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Stripe;

namespace Admin.Test.Controllers;

[ControllerCustomize(typeof(UsersController))]
[SutProviderCustomize]
public class UsersControllerTests
{
    private static void StubEditGetDependencies(SutProvider<UsersController> sutProvider, User user)
    {
        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(user.Id).Returns(user);
        sutProvider.GetDependency<ICipherRepository>()
            .GetManyByUserIdAsync(user.Id, false)
            .Returns(new List<Bit.Core.Vault.Models.Data.CipherDetails>());

        sutProvider.Sut.TempData =
            new TempDataDictionary(new DefaultHttpContext(), Substitute.For<ITempDataProvider>());
    }

    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task Edit_Get_BillingLoadThrows_StillRendersPageWithWarning(
        User user,
        SutProvider<UsersController> sutProvider)
    {
        // PM-40292: a deleted Stripe customer makes GetBillingAsync throw. The page must still
        // render so an admin can correct the Gateway Customer ID rather than being locked out.
        StubEditGetDependencies(sutProvider, user);

        sutProvider.GetDependency<IStripePaymentService>()
            .GetBillingAsync(user)
            .ThrowsAsync(new StripeException
            {
                StripeError = new StripeError { Code = StripeConstants.ErrorCodes.ResourceMissing }
            });

        var result = await sutProvider.Sut.Edit(user.Id);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<UserEditModel>(view.Model);
        Assert.Null(model.BillingInfo);
        Assert.Null(model.BillingHistoryInfo);
        Assert.True(sutProvider.Sut.TempData.ContainsKey("Warning"));
        Assert.Equal(
            "Billing information could not be loaded. The Stripe customer may have been deleted. " +
            "You can still edit the user and set a valid Gateway Customer ID.",
            (string)sutProvider.Sut.TempData["Warning"]);
    }

    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task Edit_Get_BillingHistoryLoadThrows_StillRendersPageWithWarning(
        User user,
        BillingInfo billingInfo,
        SutProvider<UsersController> sutProvider)
    {
        // PM-40292: GetBillingAsync can succeed while GetBillingHistoryAsync throws. The catch must
        // reset both values so the billing section is hidden and the page renders, rather than
        // falling through with a non-null BillingInfo and a null BillingHistoryInfo.
        StubEditGetDependencies(sutProvider, user);

        sutProvider.GetDependency<IStripePaymentService>()
            .GetBillingAsync(user)
            .Returns(billingInfo);
        sutProvider.GetDependency<IStripePaymentService>()
            .GetBillingHistoryAsync(user)
            .ThrowsAsync(new StripeException
            {
                StripeError = new StripeError { Code = StripeConstants.ErrorCodes.ResourceMissing }
            });

        var result = await sutProvider.Sut.Edit(user.Id);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<UserEditModel>(view.Model);
        Assert.Null(model.BillingInfo);
        Assert.Null(model.BillingHistoryInfo);
        Assert.True(sutProvider.Sut.TempData.ContainsKey("Warning"));
        Assert.Equal(
            "Billing information could not be loaded. The Stripe customer may have been deleted. " +
            "You can still edit the user and set a valid Gateway Customer ID.",
            (string)sutProvider.Sut.TempData["Warning"]);
    }

    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task Edit_Get_BillingLoadThrowsUnexpectedError_StillRendersPageWithErrorToast(
        User user,
        SutProvider<UsersController> sutProvider)
    {
        // PM-40292: a billing-load failure that is NOT a missing Stripe customer (resource_missing)
        // must fall through to the generic catch, which surfaces a neutral error toast rather than
        // asserting the customer was deleted.
        StubEditGetDependencies(sutProvider, user);

        sutProvider.GetDependency<IStripePaymentService>()
            .GetBillingAsync(user)
            .ThrowsAsync(new StripeException { StripeError = new StripeError { Code = "api_error" } });

        var result = await sutProvider.Sut.Edit(user.Id);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<UserEditModel>(view.Model);
        Assert.Null(model.BillingInfo);
        Assert.Null(model.BillingHistoryInfo);
        Assert.False(sutProvider.Sut.TempData.ContainsKey("Warning"));
        Assert.True(sutProvider.Sut.TempData.ContainsKey("Error"));
        Assert.Equal(
            "Billing information could not be loaded. You can still edit the user or try reloading the page. " +
            "Contact support if the problem persists.",
            (string)sutProvider.Sut.TempData["Error"]);
    }
}
