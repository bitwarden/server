using System.Security.Claims;
using Bit.Core.Auth.IdentityServer;
using Bit.Core.Auth.Repositories;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Identity.Controllers;
using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using AuthenticationOptions = Duende.IdentityServer.Configuration.AuthenticationOptions;

#nullable enable

namespace Bit.Identity.Test.Auth.Controllers;

public class SsoControllerTests
{
    private const string IdentityServerCookieScheme = "idsrv";

    private readonly IIdentityServerInteractionService _interaction = Substitute.For<IIdentityServerInteractionService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IAuthenticationService _authenticationService = Substitute.For<IAuthenticationService>();
    private readonly SsoController _sut;

    public SsoControllerTests()
    {
        _sut = new SsoController(
            _interaction,
            Substitute.For<ILogger<SsoController>>(),
            Substitute.For<ISsoConfigRepository>(),
            _userRepository,
            Substitute.For<IHttpClientFactory>());

        var schemeProvider = Substitute.For<IAuthenticationSchemeProvider>();
        schemeProvider.GetDefaultAuthenticateSchemeAsync()
            .Returns(new AuthenticationScheme(IdentityServerCookieScheme, IdentityServerCookieScheme,
                typeof(IAuthenticationHandler)));

        var services = new ServiceCollection();
        services.AddSingleton(_authenticationService);
        services.AddSingleton(schemeProvider);
        services.AddSingleton(new IdentityServerOptions
        {
            Authentication = new AuthenticationOptions { CookieAuthenticationScheme = IdentityServerCookieScheme },
        });

        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() },
        };

        var urlHelper = Substitute.For<IUrlHelper>();
        urlHelper.IsLocalUrl("~/").Returns(true);
        _sut.Url = urlHelper;

        _interaction.GetAuthorizationContextAsync(Arg.Any<string>()).Returns((AuthorizationRequest?)null);
    }

    [Theory]
    [InlineData(JwtClaimTypes.Subject)]
    [InlineData(ClaimTypes.NameIdentifier)]
    public async Task ExternalCallback_TicketFromSsoScheme_SignsInUser(string userIdClaimType)
    {
        // Arrange
        var user = new User { Id = Guid.NewGuid(), Email = "user@example.com" };
        _userRepository.GetByIdAsync(user.Id).Returns(user);
        SetExternalTicket(userIdClaimType, user.Id.ToString(),
            AuthenticationSchemes.IdentitySsoOidc);

        // Act
        var result = await _sut.ExternalCallback();

        // Assert
        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("~/", redirect.Url);
        await _authenticationService.Received(1).SignInAsync(
            Arg.Any<HttpContext>(),
            IdentityServerCookieScheme,
            Arg.Is<ClaimsPrincipal>(p => p.FindFirstValue(JwtClaimTypes.Subject) == user.Id.ToString()),
            Arg.Any<AuthenticationProperties>());
        await _authenticationService.Received(1).SignOutAsync(
            Arg.Any<HttpContext>(),
            AuthenticationSchemes.IdentityExternalCookie,
            Arg.Any<AuthenticationProperties>());
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000001")]
    [InlineData("")]
    [InlineData(null)]
    public async Task ExternalCallback_TicketFromOtherScheme_Throws(string? scheme)
    {
        // Arrange
        SetExternalTicket(JwtClaimTypes.Subject, Guid.NewGuid().ToString(), scheme);

        // Act
        var exception = await Assert.ThrowsAsync<Exception>(() => _sut.ExternalCallback());

        // Assert
        Assert.Equal("External authentication error", exception.Message);
        await _userRepository.DidNotReceiveWithAnyArgs().GetByIdAsync(default);
        await AssertDidNotSignInAsync();
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("")]
    public async Task ExternalCallback_UserIdIsNotGuid_Throws(string userId)
    {
        // Arrange
        SetExternalTicket(JwtClaimTypes.Subject, userId,
            AuthenticationSchemes.IdentitySsoOidc);

        // Act
        var exception = await Assert.ThrowsAsync<Exception>(() => _sut.ExternalCallback());

        // Assert
        Assert.Equal("Unknown userid", exception.Message);
        await _userRepository.DidNotReceiveWithAnyArgs().GetByIdAsync(default);
        await AssertDidNotSignInAsync();
    }

    /// <summary>
    /// Makes the external cookie scheme return a ticket for <paramref name="userId"/>. A null
    /// <paramref name="scheme"/> leaves the ticket without a challenge scheme item.
    /// </summary>
    private void SetExternalTicket(string userIdClaimType, string userId, string? scheme)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(userIdClaimType, userId)], "External"));
        var properties = new AuthenticationProperties { Items = { ["return_url"] = "~/" } };
        if (scheme != null)
        {
            properties.Items["scheme"] = scheme;
        }

        _authenticationService.AuthenticateAsync(
                Arg.Any<HttpContext>(),
                AuthenticationSchemes.IdentityExternalCookie)
            .Returns(AuthenticateResult.Success(new AuthenticationTicket(principal, properties,
                AuthenticationSchemes.IdentityExternalCookie)));
    }

    private async Task AssertDidNotSignInAsync() =>
        await _authenticationService.DidNotReceiveWithAnyArgs().SignInAsync(default!, default, default!, default);
}
