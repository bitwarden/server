using System.Security.Claims;
using Bit.Admin.Billing.Controllers;
using Bit.Admin.Billing.Models.SalesAssistedTrial;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.TrialInitiation.Registration;
using Bit.Core.Exceptions;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Admin.Test.Billing.Controllers;

[ControllerCustomize(typeof(SalesAssistedTrialController))]
[SutProviderCustomize]
public class SalesAssistedTrialControllerTests
{
    private const string SenderEmail = "sales.rep@bitwarden.com";

    private static SalesTrialInviteModel BuildValidModel() => new()
    {
        Email = "prospect@example.com",
        Name = "Prospect Company",
        ProductTier = ProductTierType.Enterprise,
        Products = new[] { ProductType.PasswordManager },
        TrialLength = 14,
        PaymentOptional = false
    };

    private static void SetUpAuthenticatedSender(
        SutProvider<SalesAssistedTrialController> sutProvider,
        string senderEmail = SenderEmail)
    {
        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Name, senderEmail) },
            authenticationType: "TestAuth");
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        sutProvider.Sut.ControllerContext = new ControllerContext { HttpContext = httpContext };
        sutProvider.Sut.TempData =
            new TempDataDictionary(httpContext, Substitute.For<ITempDataProvider>());
    }

    [Theory, BitAutoData]
    public void Index_Get_ReturnsViewWithEmptyModel(
        SutProvider<SalesAssistedTrialController> sutProvider)
    {
        var result = sutProvider.Sut.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.IsType<SalesTrialInviteModel>(viewResult.Model);
    }

    [Theory, BitAutoData]
    public async Task Index_Post_ValidModel_SendsInvitationLogsAndRedirects(
        SutProvider<SalesAssistedTrialController> sutProvider)
    {
        var model = BuildValidModel();
        SetUpAuthenticatedSender(sutProvider);

        var result = await sutProvider.Sut.Index(model);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(SalesAssistedTrialController.Index), redirectResult.ActionName);
        Assert.Equal("Invitation sent.", sutProvider.Sut.TempData["Success"]);

        await sutProvider.GetDependency<ISendSalesAssistedTrialInvitationCommand>()
            .Received(1)
            .HandleAsync(
                model.Email,
                model.Name,
                SenderEmail,
                model.ProductTier,
                model.Products,
                model.TrialLength,
                model.PaymentOptional);

        sutProvider.GetDependency<ILogger<SalesAssistedTrialController>>()
            .Received(1)
            .Log(
                LogLevel.Information,
                Arg.Any<EventId>(),
                Arg.Is<object>(state =>
                    state.ToString()!.Contains(SenderEmail) &&
                    state.ToString()!.Contains(model.Email)),
                null,
                Arg.Any<Func<object, Exception?, string>>());
    }

    [Theory, BitAutoData]
    public async Task Index_Post_SenderEmailSourcedFromIdentityNotForm(
        SutProvider<SalesAssistedTrialController> sutProvider)
    {
        const string identityEmail = "actual.sender@bitwarden.com";
        var model = BuildValidModel();
        // The model has no sender field; ensure the identity value is what reaches the command.
        SetUpAuthenticatedSender(sutProvider, identityEmail);

        await sutProvider.Sut.Index(model);

        await sutProvider.GetDependency<ISendSalesAssistedTrialInvitationCommand>()
            .Received(1)
            .HandleAsync(
                Arg.Any<string>(),
                Arg.Any<string?>(),
                identityEmail,
                Arg.Any<ProductTierType>(),
                Arg.Any<IEnumerable<ProductType>>(),
                Arg.Any<int>(),
                Arg.Any<bool>());
    }

    [Theory, BitAutoData]
    public async Task Index_Post_InvalidModelState_ReturnsViewWithoutCallingCommand(
        SutProvider<SalesAssistedTrialController> sutProvider)
    {
        var model = BuildValidModel();
        SetUpAuthenticatedSender(sutProvider);
        sutProvider.Sut.ModelState.AddModelError(nameof(model.Email), "The Email field is required.");

        var result = await sutProvider.Sut.Index(model);

        Assert.IsType<ViewResult>(result);
        await sutProvider.GetDependency<ISendSalesAssistedTrialInvitationCommand>()
            .DidNotReceiveWithAnyArgs()
            .HandleAsync(default!, default, default!, default, default!, default, default);
    }

    [Theory, BitAutoData]
    public async Task Index_Post_AlreadyRegisteredEmail_AddsModelErrorReturnsViewNoRedirectNoLog(
        SutProvider<SalesAssistedTrialController> sutProvider)
    {
        var model = BuildValidModel();
        SetUpAuthenticatedSender(sutProvider);

        sutProvider.GetDependency<ISendSalesAssistedTrialInvitationCommand>()
            .HandleAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<ProductTierType>(),
                Arg.Any<IEnumerable<ProductType>>(), Arg.Any<int>(), Arg.Any<bool>())
            .ThrowsAsync(new BadRequestException(
                "A Bitwarden account already exists with this email address."));

        var result = await sutProvider.Sut.Index(model);

        Assert.IsType<ViewResult>(result);
        Assert.False(sutProvider.Sut.ModelState.IsValid);
        Assert.Contains("already exists",
            sutProvider.Sut.ModelState[string.Empty]!.Errors[0].ErrorMessage);
        Assert.Null(sutProvider.Sut.TempData["Success"]);

        // BadRequestException is an expected validation outcome — no log entry.
        sutProvider.GetDependency<ILogger<SalesAssistedTrialController>>()
            .DidNotReceive()
            .Log(
                Arg.Any<LogLevel>(),
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>());
    }

    [Theory, BitAutoData]
    public async Task Index_Post_UnexpectedException_LogsErrorAddsGenericErrorReturnsView(
        SutProvider<SalesAssistedTrialController> sutProvider)
    {
        var model = BuildValidModel();
        SetUpAuthenticatedSender(sutProvider);

        sutProvider.GetDependency<ISendSalesAssistedTrialInvitationCommand>()
            .HandleAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<ProductTierType>(),
                Arg.Any<IEnumerable<ProductType>>(), Arg.Any<int>(), Arg.Any<bool>())
            .ThrowsAsync(new Exception("Unexpected failure"));

        var result = await sutProvider.Sut.Index(model);

        Assert.IsType<ViewResult>(result);
        Assert.False(sutProvider.Sut.ModelState.IsValid);
        Assert.Contains("error occurred",
            sutProvider.Sut.ModelState[string.Empty]!.Errors[0].ErrorMessage);

        sutProvider.GetDependency<ILogger<SalesAssistedTrialController>>()
            .Received(1)
            .Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception?, string>>());
    }

    [Theory, BitAutoData]
    public async Task Index_Post_ZeroTrialLengthPaymentNotOptional_ReachesCommand(
        SutProvider<SalesAssistedTrialController> sutProvider)
    {
        var model = BuildValidModel();
        model.TrialLength = 0;
        model.PaymentOptional = false;
        SetUpAuthenticatedSender(sutProvider);

        var result = await sutProvider.Sut.Index(model);

        Assert.IsType<RedirectToActionResult>(result);
        await sutProvider.GetDependency<ISendSalesAssistedTrialInvitationCommand>()
            .Received(1)
            .HandleAsync(
                model.Email,
                model.Name,
                SenderEmail,
                model.ProductTier,
                model.Products,
                0,
                false);
    }

    [Theory, BitAutoData]
    public async Task Index_Post_ZeroTrialLengthPaymentOptional_CommandThrowsBadRequest_AddsModelError(
        SutProvider<SalesAssistedTrialController> sutProvider)
    {
        var model = BuildValidModel();
        model.TrialLength = 0;
        model.PaymentOptional = true;
        SetUpAuthenticatedSender(sutProvider);

        sutProvider.GetDependency<ISendSalesAssistedTrialInvitationCommand>()
            .HandleAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<ProductTierType>(),
                Arg.Any<IEnumerable<ProductType>>(), 0, true)
            .ThrowsAsync(new BadRequestException(
                "Payment cannot be optional when there is no trial period."));

        var result = await sutProvider.Sut.Index(model);

        Assert.IsType<ViewResult>(result);
        Assert.False(sutProvider.Sut.ModelState.IsValid);
        Assert.Contains("Payment cannot be optional",
            sutProvider.Sut.ModelState[string.Empty]!.Errors[0].ErrorMessage);
    }
}
