using System.Text.Json;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.Helpers;
using static Bit.Identity.IntegrationTest.Helpers.TwoFactorLoginTestHelpers;

namespace Bit.Identity.IntegrationTest.TokenEndpoint.TwoFactor;

public class TwoFactorLoginRecoveryCodeTests : IClassFixture<IdentityApplicationFactory>
{
    private readonly IdentityApplicationFactory _factory;

    public TwoFactorLoginRecoveryCodeTests(IdentityApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// The recovery code completes login and removes the user's two-factor providers.
    /// </summary>
    [Fact]
    public async Task TokenEndpoint_GrantTypePassword_ValidRecoveryCode_SuccessAndTwoFactorRemoved()
    {
        // Arrange
        const string recoveryCode = "abcdefghijklmnopqrstuvwxyz012345";
        var email = NewUniqueEmail();
        await CreateUserAsync(_factory, email, BuildUserEmailTwoFactor(email));
        var userRepository = _factory.GetService<IUserRepository>();
        var user = await userRepository.GetByEmailAsync(email);
        user.TwoFactorRecoveryCode = recoveryCode;
        await userRepository.ReplaceAsync(user);

        // Act
        var context = await _factory.ContextFromPasswordWithTwoFactorAsync(email, TestPassword,
            twoFactorProviderType: ProviderKey(TwoFactorProviderType.RecoveryCode), twoFactorToken: recoveryCode);

        // Assert
        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        AssertHelper.AssertJsonProperty(body.RootElement, "access_token", JsonValueKind.String);
        Assert.Null((await userRepository.GetByEmailAsync(email)).TwoFactorProviders);
    }
}
