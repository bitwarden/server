using System.Security.Claims;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains.Interfaces;
using Bit.Core.Auth.UserFeatures.UserEmail;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Authorization;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Bit.Core.Test.Auth.UserFeatures.UserEmail;

[SutProviderCustomize]
public class SelfServiceChangeEmailCommandTests
{
    // Same-domain pair: lets tests bypass the org-domain policy gate via the
    // EmailValidation.GetDomain short-circuit in EnsureNewEmailDomainAllowedAsync.
    private const string _currentEmail = "old@example.com";
    private const string _newEmail = "new@example.com";
    private const string _masterPasswordHash = "master-password-hash";
    private const string _token = "change-email-token";
    private static readonly string _changeEmailTokenProvider = TokenOptions.DefaultEmailProvider;
    private static readonly string _changeEmailPurpose = "ChangeEmail:" + _newEmail;

    [Theory, BitAutoData]
    public async Task ChangeEmailAsync_Success_DelegatesToChangeEmailCommand(User user)
    {
        var userManager = SubstituteUserManager();
        userManager.VerifyUserTokenAsync(user, _changeEmailTokenProvider, _changeEmailPurpose, _token)
            .Returns(true);
        var sutProvider = CreateSutProvider(userManager);
        AllowKeyConnector(sutProvider, user);
        sutProvider.GetDependency<IUserService>()
            .CheckPasswordAsync(user, _masterPasswordHash)
            .Returns(true);

        await sutProvider.Sut.ChangeEmailAsync(user, _masterPasswordHash, _newEmail, _token);

        await sutProvider.GetDependency<IChangeEmailCommand>().Received(1)
            .ChangeEmailAsync(user, _newEmail);
    }

    [Theory, BitAutoData]
    public async Task ChangeEmailAsync_KeyConnectorDenied_ThrowsAndShortCircuits(User user)
    {
        var userManager = SubstituteUserManager();
        var sutProvider = CreateSutProvider(userManager);
        DenyKeyConnector(sutProvider, user);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ChangeEmailAsync(user, _masterPasswordHash, _newEmail, _token));
        Assert.Equal("You cannot change your email when using Key Connector.", ex.Message);

        // KeyConnector denial short-circuits ahead of password verification and token issuance.
        await sutProvider.GetDependency<IUserService>().DidNotReceiveWithAnyArgs()
            .CheckPasswordAsync(default!, default!);
        await sutProvider.GetDependency<IChangeEmailCommand>().DidNotReceiveWithAnyArgs()
            .ChangeEmailAsync(default!, default!);
    }

    [Theory, BitAutoData]
    public async Task ChangeEmailAsync_WrongMasterPassword_ThrowsAndShortCircuits(User user)
    {
        var userManager = SubstituteUserManager();
        var sutProvider = CreateSutProvider(userManager);
        AllowKeyConnector(sutProvider, user);
        sutProvider.GetDependency<IUserService>()
            .CheckPasswordAsync(user, _masterPasswordHash)
            .Returns(false);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ChangeEmailAsync(user, _masterPasswordHash, _newEmail, _token));
        Assert.True(ex.ModelState!.ContainsKey("MasterPasswordHash"));

        // No further work happens after a password mismatch. We can't assert against the real-ish
        // UserManager substitute's calls here because AutoNSubstitute does not proxy concrete
        // classes like UserManager<User>; the assertion below covers the post-verification side
        // effect that would have followed token verification.
        await sutProvider.GetDependency<IChangeEmailCommand>().DidNotReceiveWithAnyArgs()
            .ChangeEmailAsync(default!, default!);
    }

    [Theory, BitAutoData]
    public async Task ChangeEmailAsync_InvalidToken_ThrowsAndShortCircuits(User user)
    {
        var userManager = SubstituteUserManager();
        userManager.VerifyUserTokenAsync(user, _changeEmailTokenProvider, _changeEmailPurpose, _token)
            .Returns(false);
        var sutProvider = CreateSutProvider(userManager);
        AllowKeyConnector(sutProvider, user);
        sutProvider.GetDependency<IUserService>()
            .CheckPasswordAsync(user, _masterPasswordHash)
            .Returns(true);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ChangeEmailAsync(user, _masterPasswordHash, _newEmail, _token));
        Assert.True(ex.ModelState!.ContainsKey("Token"));

        await sutProvider.GetDependency<IChangeEmailCommand>().DidNotReceiveWithAnyArgs()
            .ChangeEmailAsync(default!, default!);
    }

    [Theory, BitAutoData]
    public async Task ChangeEmailAsync_UsesConfiguredChangeEmailTokenProvider(User user)
    {
        // The token provider name is read off IdentityOptions, not hardcoded; this guards against
        // accidental regressions when the provider is rewired in SharedWeb registration.
        const string customProvider = "Custom:ChangeEmailProvider";
        var userManager = SubstituteUserManager();
        userManager.VerifyUserTokenAsync(user, customProvider, _changeEmailPurpose, _token)
            .Returns(true);
        var sutProvider = CreateSutProvider(userManager, customProvider);
        AllowKeyConnector(sutProvider, user);
        sutProvider.GetDependency<IUserService>()
            .CheckPasswordAsync(user, _masterPasswordHash)
            .Returns(true);

        await sutProvider.Sut.ChangeEmailAsync(user, _masterPasswordHash, _newEmail, _token);

        await userManager.Received(1)
            .VerifyUserTokenAsync(user, customProvider, _changeEmailPurpose, _token);
    }

    [Theory, BitAutoData]
    public async Task InitiateChangeEmailAsync_NewEmailAvailable_SendsChangeEmailWithToken(User user)
    {
        user.Email = _currentEmail;
        var userManager = SubstituteUserManager();
        userManager.GenerateChangeEmailTokenAsync(user, _newEmail).Returns(_token);
        var sutProvider = CreateSutProvider(userManager);
        AllowKeyConnector(sutProvider, user);
        sutProvider.GetDependency<IUserService>()
            .CheckPasswordAsync(user, _masterPasswordHash)
            .Returns(true);
        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(_newEmail)
            .Returns((User?)null);

        await sutProvider.Sut.InitiateChangeEmailAsync(user, _masterPasswordHash, _newEmail);

        await sutProvider.GetDependency<IMailService>().Received(1)
            .SendChangeEmailEmailAsync(_newEmail, _token);
        await sutProvider.GetDependency<IMailService>().DidNotReceiveWithAnyArgs()
            .SendChangeEmailAlreadyExistsEmailAsync(default!, default!);
        await sutProvider.GetDependency<IOrganizationDomainAllowEmailChangeQuery>().Received(1)
            .IsAllowedAsync(user, _newEmail);
    }

    [Theory, BitAutoData]
    public async Task InitiateChangeEmailAsync_NewEmailInUse_NotifiesCurrentEmailWithoutIssuingToken(User user)
    {
        user.Email = _currentEmail;
        var userManager = SubstituteUserManager();
        var sutProvider = CreateSutProvider(userManager);
        AllowKeyConnector(sutProvider, user);
        sutProvider.GetDependency<IUserService>()
            .CheckPasswordAsync(user, _masterPasswordHash)
            .Returns(true);
        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(_newEmail)
            .Returns(new User { Email = _newEmail });

        // Completes without throwing so the API surface does not leak whether the new email is
        // already registered; instead the existing account is notified out-of-band.
        await sutProvider.Sut.InitiateChangeEmailAsync(user, _masterPasswordHash, _newEmail);

        await sutProvider.GetDependency<IMailService>().Received(1)
            .SendChangeEmailAlreadyExistsEmailAsync(user.Email, _newEmail);
        await sutProvider.GetDependency<IMailService>().DidNotReceiveWithAnyArgs()
            .SendChangeEmailEmailAsync(default!, default!);
        await userManager.DidNotReceiveWithAnyArgs()
            .GenerateChangeEmailTokenAsync(default!, default!);
    }

    [Theory, BitAutoData]
    public async Task InitiateChangeEmailAsync_KeyConnectorDenied_ThrowsAndShortCircuits(User user)
    {
        user.Email = _currentEmail;
        var userManager = SubstituteUserManager();
        var sutProvider = CreateSutProvider(userManager);
        DenyKeyConnector(sutProvider, user);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.InitiateChangeEmailAsync(user, _masterPasswordHash, _newEmail));
        Assert.Equal("You cannot change your email when using Key Connector.", ex.Message);

        await sutProvider.GetDependency<IUserService>().DidNotReceiveWithAnyArgs()
            .CheckPasswordAsync(default!, default!);
        await sutProvider.GetDependency<IOrganizationDomainAllowEmailChangeQuery>().DidNotReceiveWithAnyArgs()
            .IsAllowedAsync(default!, default!);
        await sutProvider.GetDependency<IUserRepository>().DidNotReceiveWithAnyArgs()
            .GetByEmailAsync(default!);
        await sutProvider.GetDependency<IMailService>().DidNotReceiveWithAnyArgs()
            .SendChangeEmailEmailAsync(default!, default!);
        await sutProvider.GetDependency<IMailService>().DidNotReceiveWithAnyArgs()
            .SendChangeEmailAlreadyExistsEmailAsync(default!, default!);
    }

    [Theory, BitAutoData]
    public async Task InitiateChangeEmailAsync_WrongMasterPassword_ThrowsAndShortCircuits(User user)
    {
        user.Email = _currentEmail;
        var userManager = SubstituteUserManager();
        var sutProvider = CreateSutProvider(userManager);
        AllowKeyConnector(sutProvider, user);
        sutProvider.GetDependency<IUserService>()
            .CheckPasswordAsync(user, _masterPasswordHash)
            .Returns(false);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.InitiateChangeEmailAsync(user, _masterPasswordHash, _newEmail));
        Assert.True(ex.ModelState!.ContainsKey("MasterPasswordHash"));

        await sutProvider.GetDependency<IOrganizationDomainAllowEmailChangeQuery>().DidNotReceiveWithAnyArgs()
            .IsAllowedAsync(default!, default!);
        await sutProvider.GetDependency<IUserRepository>().DidNotReceiveWithAnyArgs()
            .GetByEmailAsync(default!);
        await sutProvider.GetDependency<IMailService>().DidNotReceiveWithAnyArgs()
            .SendChangeEmailEmailAsync(default!, default!);
        await sutProvider.GetDependency<IMailService>().DidNotReceiveWithAnyArgs()
            .SendChangeEmailAlreadyExistsEmailAsync(default!, default!);
    }

    [Theory, BitAutoData]
    public async Task InitiateChangeEmailAsync_DomainGateThrows_PropagatesAndDoesNotIssueToken(User user)
    {
        // Domain-policy details (denial reasons, message text, same-domain short-circuit) belong
        // to OrganizationDomainAllowEmailChangeQuery.IsAllowedAsync and are covered there.
        // This test just locks in that InitiateChangeEmailAsync defers to that gate and propagates
        // failures without issuing a change-email token or notifying anyone.
        user.Email = _currentEmail;
        const string newEmail = "new@other-domain.com";
        var userManager = SubstituteUserManager();
        var sutProvider = CreateSutProvider(userManager);
        AllowKeyConnector(sutProvider, user);
        sutProvider.GetDependency<IUserService>()
            .CheckPasswordAsync(user, _masterPasswordHash)
            .Returns(true);
        var thrown = new BadRequestException("Domain not allowed.");
        sutProvider.GetDependency<IOrganizationDomainAllowEmailChangeQuery>()
            .IsAllowedAsync(user, newEmail)
            .Throws(thrown);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.InitiateChangeEmailAsync(user, _masterPasswordHash, newEmail));
        Assert.Same(thrown, ex);

        await sutProvider.GetDependency<IUserRepository>().DidNotReceiveWithAnyArgs()
            .GetByEmailAsync(default!);
        await sutProvider.GetDependency<IMailService>().DidNotReceiveWithAnyArgs()
            .SendChangeEmailEmailAsync(default!, default!);
        await sutProvider.GetDependency<IMailService>().DidNotReceiveWithAnyArgs()
            .SendChangeEmailAlreadyExistsEmailAsync(default!, default!);
        await userManager.DidNotReceiveWithAnyArgs()
            .GenerateChangeEmailTokenAsync(default!, default!);
    }

    private static void AllowKeyConnector(SutProvider<SelfServiceChangeEmailCommand> sutProvider, User user) =>
        SetKeyConnectorAuthorization(sutProvider, user, AuthorizationResult.Success());

    private static void DenyKeyConnector(SutProvider<SelfServiceChangeEmailCommand> sutProvider, User user) =>
        SetKeyConnectorAuthorization(sutProvider, user, AuthorizationResult.Failed());

    private static void SetKeyConnectorAuthorization(
        SutProvider<SelfServiceChangeEmailCommand> sutProvider,
        User user,
        AuthorizationResult result)
    {
        var principal = new ClaimsPrincipal();
        sutProvider.GetDependency<ICurrentContext>().HttpContext.User.Returns(principal);
        // IAuthorizationService.AuthorizeAsync(principal, resource, requirement) is an extension
        // that wraps the requirement in an array and calls the IEnumerable overload — substitute
        // the underlying method so NSubstitute returns our result instead of null.
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(principal, user, Arg.Is<IEnumerable<IAuthorizationRequirement>>(
                requirements => requirements.Contains(KeyConnectorOperations.Use)))
            .Returns(result);
    }

    private static SutProvider<SelfServiceChangeEmailCommand> CreateSutProvider(
        UserManager<User> userManager,
        string? changeEmailTokenProvider = null)
    {
        changeEmailTokenProvider ??= _changeEmailTokenProvider;
        var options = new IdentityOptions();
        options.Tokens.ChangeEmailTokenProvider = changeEmailTokenProvider;

        return new SutProvider<SelfServiceChangeEmailCommand>()
            .SetDependency<UserManager<User>>(userManager)
            .SetDependency<IOptions<IdentityOptions>>(Options.Create(options))
            .Create();
    }

    private static UserManager<User> SubstituteUserManager()
    {
        return Substitute.For<UserManager<User>>(
            Substitute.For<IUserStore<User>>(),
            Substitute.For<IOptions<IdentityOptions>>(),
            Substitute.For<IPasswordHasher<User>>(),
            Enumerable.Empty<IUserValidator<User>>(),
            Enumerable.Empty<IPasswordValidator<User>>(),
            Substitute.For<ILookupNormalizer>(),
            Substitute.For<IdentityErrorDescriber>(),
            Substitute.For<IServiceProvider>(),
            Substitute.For<ILogger<UserManager<User>>>());
    }
}
