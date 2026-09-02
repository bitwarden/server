using System.Security.Claims;
using Bit.Admin.Auth.Controllers;
using Bit.Admin.Auth.Models.SalesAssistedTrial;
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

namespace Admin.Test.Auth.Controllers;

[ControllerCustomize(typeof(SalesAssistedTrialController))]
[SutProviderCustomize]
public class SalesAssistedTrialControllerTests
{
    private const string SenderEmail = "sales.rep@bitwarden.com";

    private static SalesAssistedTrialInviteModel BuildValidModel() => new()
    {
        Email = "prospect@example.com",
        Name = "Prospect Company",
        ProductTier = ProductTierType.Enterprise,
        Product = ProductType.PasswordManager,
        TrialLength = 14
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
    public void Index_Get_ReturnsViewWithSensibleDefaults(
        SutProvider<SalesAssistedTrialController> sutProvider)
    {
        var result = sutProvider.Sut.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<SalesAssistedTrialInviteModel>(viewResult.Model);
        Assert.Equal(ProductTierType.Enterprise, model.ProductTier);
        Assert.Equal(ProductType.PasswordManager, model.Product);
        Assert.Equal(30, model.TrialLength);
    }

    [Theory, BitAutoData]
    public async Task Index_Post_ValidModel_SendsInvitationAndRedirects(
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
                Arg.Is<IEnumerable<ProductType>>(products => products.SequenceEqual(new[] { model.Product })),
                model.TrialLength);
    }

    [Theory, BitAutoData]
    public async Task Index_Post_SenderEmailSourcedFromIdentityNotForm(
        SutProvider<SalesAssistedTrialController> sutProvider)
    {
        const string identityEmail = "actual.sender@bitwarden.com";
        var model = BuildValidModel();
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
                Arg.Any<int>());
    }

    [Theory, BitAutoData]
    public async Task Index_Post_InvalidModelState_ReturnsViewWithoutCallingCommand(
        SutProvider<SalesAssistedTrialController> sutProvider)
    {
        var model = BuildValidModel();
        model.Product = ProductType.SecretsManager;
        SetUpAuthenticatedSender(sutProvider);
        sutProvider.Sut.ModelState.AddModelError(nameof(model.Email), "The Email field is required.");

        var result = await sutProvider.Sut.Index(model);

        var viewResult = Assert.IsType<ViewResult>(result);
        // Ensure when a model is returned to the view for validation errors (POST round-trip)
        // that user's choices are persisted; the defaults do not change their prior selections.
        var redisplayedModel = Assert.IsType<SalesAssistedTrialInviteModel>(viewResult.Model);
        Assert.Equal(model.Product, redisplayedModel.Product);

        await sutProvider.GetDependency<ISendSalesAssistedTrialInvitationCommand>()
            .DidNotReceiveWithAnyArgs()
            .HandleAsync(default!, default, default!, default, default!, default);
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
                Arg.Any<IEnumerable<ProductType>>(), Arg.Any<int>())
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
    public async Task Index_Post_UnexpectedException_LogsExceptionTypeAddsExceptionMessageReturnsView(
        SutProvider<SalesAssistedTrialController> sutProvider)
    {
        var model = BuildValidModel();
        SetUpAuthenticatedSender(sutProvider);

        sutProvider.GetDependency<ISendSalesAssistedTrialInvitationCommand>()
            .HandleAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<ProductTierType>(),
                Arg.Any<IEnumerable<ProductType>>(), Arg.Any<int>())
            .ThrowsAsync(new Exception("Unexpected failure"));

        var result = await sutProvider.Sut.Index(model);

        Assert.IsType<ViewResult>(result);
        Assert.False(sutProvider.Sut.ModelState.IsValid);
        Assert.Contains("Unexpected failure",
            sutProvider.Sut.ModelState[string.Empty]!.Errors[0].ErrorMessage);

        sutProvider.GetDependency<ILogger<SalesAssistedTrialController>>()
            .Received(1)
            .Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Is<object>(state =>
                    state.ToString()!.Contains(nameof(Exception)) &&
                    !state.ToString()!.Contains("Unexpected failure")),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>());
    }
}
