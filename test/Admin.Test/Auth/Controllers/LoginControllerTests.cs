using Bit.Admin;
using Bit.Admin.Auth.Controllers;
using Bit.Admin.Auth.IdentityServer;
using Bit.Admin.Auth.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Admin.Test.Auth.Controllers;

public class LoginControllerTests
{
    [Fact]
    public void Sso_ReturnsNotFound_WhenOidcDisabled()
    {
        var controller = BuildController(oidcEnabled: false);

        var result = controller.Sso(returnUrl: "/home");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task SsoSignIn_ReturnsNotFound_WhenOidcDisabled()
    {
        var controller = BuildController(oidcEnabled: false);

        var result = await controller.SsoSignIn(returnUrl: "/home");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task SsoSignIn_RedirectsWithError_WhenRemoteErrorProvided()
    {
        var controller = BuildController(oidcEnabled: true);

        var result = await controller.SsoSignIn(returnUrl: "/home", remoteError: "access_denied");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal(5, redirect.RouteValues!["error"]);
    }

    [Fact]
    public void Index_Get_SetsSsoEnabledFromSettings()
    {
        var controller = BuildController(oidcEnabled: true);

        var result = controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<LoginModel>(view.Model);
        Assert.True(model.SsoEnabled);
        Assert.Equal("SSO", model.SsoDisplayName);
    }

    [Fact]
    public void Index_Get_SetsSsoDisabled_WhenOidcMissing()
    {
        var controller = BuildController(oidcEnabled: false);

        var result = controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<LoginModel>(view.Model);
        Assert.False(model.SsoEnabled);
    }

    private static LoginController BuildController(bool oidcEnabled)
    {
        var settings = new AdminSettings();
        if (oidcEnabled)
        {
            settings.Oidc = new AdminSettings.OidcSettings
            {
                Authority = "https://idp.example.com",
                ClientId = "admin-portal",
                ClientSecret = "supersecret"
            };
        }
        else
        {
            settings.Oidc = null;
        }

        var userStore = Substitute.For<IUserStore<IdentityUser>>();
        var userManager = Substitute.For<UserManager<IdentityUser>>(
            userStore,
            Substitute.For<IOptions<IdentityOptions>>(),
            Substitute.For<IPasswordHasher<IdentityUser>>(),
            Enumerable.Empty<IUserValidator<IdentityUser>>(),
            Enumerable.Empty<IPasswordValidator<IdentityUser>>(),
            Substitute.For<ILookupNormalizer>(),
            Substitute.For<IdentityErrorDescriber>(),
            Substitute.For<IServiceProvider>(),
            Substitute.For<ILogger<UserManager<IdentityUser>>>());

        var signInManager = Substitute.For<PasswordlessSignInManager<IdentityUser>>(
            userManager,
            Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<IdentityUser>>(),
            Substitute.For<IOptions<IdentityOptions>>(),
            Substitute.For<ILogger<SignInManager<IdentityUser>>>(),
            Substitute.For<Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider>(),
            Substitute.For<IUserConfirmation<IdentityUser>>(),
            Substitute.For<Bit.Core.Services.IMailService>());

        return new LoginController(signInManager, userManager, Options.Create(settings));
    }
}
