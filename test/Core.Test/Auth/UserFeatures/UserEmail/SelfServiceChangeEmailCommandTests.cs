using Bit.Core.Auth.UserFeatures.UserEmail;
using Bit.Core.Entities;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.UserFeatures.UserEmail;

[SutProviderCustomize]
public class SelfServiceChangeEmailCommandTests
{
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
        sutProvider.GetDependency<IUserService>()
            .CheckPasswordAsync(user, _masterPasswordHash)
            .Returns(true);

        var result = await sutProvider.Sut.ChangeEmailAsync(user, _masterPasswordHash, _newEmail, _token);

        Assert.True(result.Succeeded);
        await sutProvider.GetDependency<IChangeEmailCommand>().Received(1)
            .ChangeEmailAsync(user, _newEmail);
    }

    [Theory, BitAutoData]
    public async Task ChangeEmailAsync_WrongMasterPassword_ReturnsPasswordMismatchAndShortCircuits(User user)
    {
        var userManager = SubstituteUserManager();
        var sutProvider = CreateSutProvider(userManager);
        sutProvider.GetDependency<IUserService>()
            .CheckPasswordAsync(user, _masterPasswordHash)
            .Returns(false);

        var result = await sutProvider.Sut.ChangeEmailAsync(user, _masterPasswordHash, _newEmail, _token);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Code == new IdentityErrorDescriber().PasswordMismatch().Code);

        // No further work happens after a password mismatch. We can't assert against the real-ish
        // UserManager substitute's calls here because AutoNSubstitute does not proxy concrete
        // classes like UserManager<User>; the assertion below covers the post-verification side
        // effect that would have followed token verification.
        await sutProvider.GetDependency<IChangeEmailCommand>().DidNotReceiveWithAnyArgs()
            .ChangeEmailAsync(default!, default!);
    }

    [Theory, BitAutoData]
    public async Task ChangeEmailAsync_InvalidToken_ReturnsInvalidTokenAndShortCircuits(User user)
    {
        var userManager = SubstituteUserManager();
        userManager.VerifyUserTokenAsync(user, _changeEmailTokenProvider, _changeEmailPurpose, _token)
            .Returns(false);
        var sutProvider = CreateSutProvider(userManager);
        sutProvider.GetDependency<IUserService>()
            .CheckPasswordAsync(user, _masterPasswordHash)
            .Returns(true);

        var result = await sutProvider.Sut.ChangeEmailAsync(user, _masterPasswordHash, _newEmail, _token);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Code == new IdentityErrorDescriber().InvalidToken().Code);

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
        sutProvider.GetDependency<IUserService>()
            .CheckPasswordAsync(user, _masterPasswordHash)
            .Returns(true);

        var result = await sutProvider.Sut.ChangeEmailAsync(user, _masterPasswordHash, _newEmail, _token);

        Assert.True(result.Succeeded);
        await userManager.Received(1)
            .VerifyUserTokenAsync(user, customProvider, _changeEmailPurpose, _token);
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
