using System.Security.Claims;
using Bit.Core.Auth.IdentityServer;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Identity.Controllers;
using Bit.Identity.IdentityServer.ClientProviders;
using Bit.Identity.Models.Response.Accounts;
using Duende.IdentityModel;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Identity.Test.Controllers;

public class AccountDeletionControllerTests : IDisposable
{
    private const string Token = "a-token";
    private const int InternalPort = 33657;

    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUserService _userService = Substitute.For<IUserService>();
    private readonly ITokenValidator _tokenValidator = Substitute.For<ITokenValidator>();
    private readonly GlobalSettings _globalSettings = Substitute.For<GlobalSettings>();
    private readonly ILogger<AccountDeletionController> _logger = Substitute.For<ILogger<AccountDeletionController>>();
    private readonly AccountDeletionController _sut;

    public AccountDeletionControllerTests()
    {
        _globalSettings.AccountDeletionClientSecret.Returns("a-secret");
        _globalSettings.AccountDeletionPort.Returns(InternalPort);
        _sut = new AccountDeletionController(_userRepository, _userService, _tokenValidator, _globalSettings, _logger)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
        _sut.HttpContext.Connection.LocalPort = InternalPort;
        Bearer(Token);
        Validates(Token, $"{AccountDeletionClientProvider.Prefix}.other-service");
    }

    public void Dispose() => _sut.Dispose();

    [Fact]
    public async Task Deletes_the_named_account_as_its_holder_s_own_deletion_does()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "person@example.com" };
        _userRepository.GetByIdAsync(user.Id).Returns(user);
        _userService.DeleteAsync(user).Returns(IdentityResult.Success);

        var result = await _sut.DeleteAsync(user.Id);

        var answer = Assert.IsType<AccountDeletionResponseModel>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.True(answer.Deleted);
        Assert.Equal("accountDeletion", answer.Object);
        await _userService.Received(1).DeleteAsync(user);
        await _userService.DidNotReceive().DeleteAsync(Arg.Any<User>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Leaves_the_gateway_alone_as_the_copy_s_subscription_may_be_another_s_now()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Gateway = GatewayType.Stripe,
            GatewayCustomerId = "cus_a",
            GatewaySubscriptionId = "sub_a",
        };
        _userRepository.GetByIdAsync(user.Id).Returns(user);
        _userService.DeleteAsync(user).Returns(IdentityResult.Success);

        await _sut.DeleteAsync(user.Id);

        await _userService.Received(1).DeleteAsync(Arg.Is<User>(each =>
            each.Gateway == null && each.GatewayCustomerId == null && each.GatewaySubscriptionId == null));
    }

    [Fact]
    public async Task Answers_not_deleted_for_an_account_it_does_not_hold()
    {
        _userRepository.GetByIdAsync(Arg.Any<Guid>()).ReturnsNull();

        var result = await _sut.DeleteAsync(Guid.NewGuid());

        var answer = Assert.IsType<AccountDeletionResponseModel>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.False(answer.Deleted);
        await _userService.DidNotReceiveWithAnyArgs().DeleteAsync(default(User)!);
    }

    [Fact]
    public async Task Refuses_what_the_holder_s_own_deletion_refuses_in_its_words()
    {
        var user = new User { Id = Guid.NewGuid() };
        _userRepository.GetByIdAsync(user.Id).Returns(user);
        _userService.DeleteAsync(user).Returns(IdentityResult.Failed(new IdentityError
        {
            Description = "Cannot delete this user because it is the sole owner of at least one organization. Please delete these organizations or upgrade another user.",
        }));

        var refused = await Assert.ThrowsAsync<BadRequestException>(() => _sut.DeleteAsync(user.Id));

        Assert.Equal(
            "Cannot delete this user because it is the sole owner of at least one organization. Please delete these organizations or upgrade another user.",
            refused.Message);
    }

    [Fact]
    public async Task Refuses_a_claimed_account_as_the_holder_s_own_deletion_does()
    {
        var user = new User { Id = Guid.NewGuid() };
        _userRepository.GetByIdAsync(user.Id).Returns(user);
        _userService.DeleteAsync(user).Throws(new BadRequestException("Cannot delete accounts owned by an organization. Contact your organization administrator for additional details."));

        await Assert.ThrowsAsync<BadRequestException>(() => _sut.DeleteAsync(user.Id));
    }

    [Fact]
    public async Task Is_not_there_when_no_client_may_use_it()
    {
        _globalSettings.AccountDeletionClientSecret.Returns((string)null!);

        var result = await _sut.DeleteAsync(Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result);
        await _userRepository.DidNotReceiveWithAnyArgs().GetByIdAsync(default);
    }

    [Fact]
    public async Task Is_not_there_without_an_internal_listener()
    {
        _globalSettings.AccountDeletionPort.Returns((int?)null);

        var result = await _sut.DeleteAsync(Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result);
        await _userRepository.DidNotReceiveWithAnyArgs().GetByIdAsync(default);
    }

    [Theory]
    [InlineData(33656)]
    [InlineData(0)]
    public async Task Is_not_there_on_any_listener_but_the_internal_one(int port)
    {
        _sut.HttpContext.Connection.LocalPort = port;

        var result = await _sut.DeleteAsync(Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result);
        await _tokenValidator.DidNotReceiveWithAnyArgs().ValidateAccessTokenAsync(default!, default);
        await _userRepository.DidNotReceiveWithAnyArgs().GetByIdAsync(default);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Basic YTpi")]
    [InlineData("Bearer ")]
    public async Task Refuses_a_request_without_a_bearer_token(string? authorization)
    {
        _sut.HttpContext.Request.Headers.Authorization = authorization;

        var result = await _sut.DeleteAsync(Guid.NewGuid());

        Assert.IsType<UnauthorizedResult>(result);
        await _userRepository.DidNotReceiveWithAnyArgs().GetByIdAsync(default);
        await _userService.DidNotReceiveWithAnyArgs().DeleteAsync(default(User)!);
    }

    [Fact]
    public async Task Refuses_a_token_that_does_not_validate_for_the_deletion_scope()
    {
        // Another internal client's token, say: it was issued for internal.notifications alone.
        Bearer("another-client-token");
        _tokenValidator.ValidateAccessTokenAsync("another-client-token", ApiScopes.InternalAccountDeletion)
            .Returns(new TokenValidationResult { IsError = true, Error = "insufficient_scope" });

        var result = await _sut.DeleteAsync(Guid.NewGuid());

        Assert.IsType<UnauthorizedResult>(result);
        await _userService.DidNotReceiveWithAnyArgs().DeleteAsync(default(User)!);
    }

    [Theory]
    [InlineData("web")]
    [InlineData("internal.notifications")]
    [InlineData("installation.other-service")]
    [InlineData("deletionother-service")]
    public async Task Refuses_a_token_issued_to_any_other_client(string client)
    {
        Validates(Token, client);

        var result = await _sut.DeleteAsync(Guid.NewGuid());

        Assert.IsType<UnauthorizedResult>(result);
        await _userService.DidNotReceiveWithAnyArgs().DeleteAsync(default(User)!);
    }

    [Fact]
    public void Answers_on_a_guid_route_alone()
    {
        var route = typeof(AccountDeletionController).GetMethod(nameof(AccountDeletionController.DeleteAsync))!
            .GetCustomAttributes(typeof(HttpDeleteAttribute), false).Cast<HttpDeleteAttribute>().Single();

        Assert.Equal("{id:guid}", route.Template);
    }

    private void Bearer(string token) =>
        _sut.HttpContext.Request.Headers.Authorization = $"Bearer {token}";

    private void Validates(string token, string client) =>
        _tokenValidator.ValidateAccessTokenAsync(token, ApiScopes.InternalAccountDeletion)
            .Returns(new TokenValidationResult
            {
                IsError = false,
                Claims = [new Claim(JwtClaimTypes.ClientId, client), new Claim(JwtClaimTypes.Scope, ApiScopes.InternalAccountDeletion)],
            });
}
