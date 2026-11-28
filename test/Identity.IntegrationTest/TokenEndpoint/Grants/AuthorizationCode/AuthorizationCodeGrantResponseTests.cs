using System.Text.Json;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Kdf;
using Bit.Core.Repositories;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.Helpers;
using Xunit;
using static Bit.Identity.IntegrationTest.Helpers.AuthorizationCodeGrantTestHelpers;
using static Bit.Identity.IntegrationTest.Helpers.TokenResponseAssertions;
using static Bit.Identity.IntegrationTest.Helpers.UserTestHelpers;

#nullable enable

namespace Bit.Identity.IntegrationTest.TokenEndpoint.Grants.AuthorizationCode;

/// <summary>
/// Integration tests for a successful <c>authorization_code</c> grant: the response body, and what an SSO login
/// records for the user. The tests store the authorization code directly instead of running the SSO login, so they
/// cover only the token request.
/// </summary>
public class AuthorizationCodeGrantResponseTests
{
    /// <summary>
    /// An SSO login returns the tokens and the account data that the client needs to unlock the vault.
    /// </summary>
    [Fact]
    public async Task AuthorizationCodeGrant_Success_ReturnsLoginResponse()
    {
        using var factory = new IdentityApplicationFactory();
        var (user, code) = await RegisterSsoUserWithAuthorizationCodeAsync(factory);

        var context = await PostAuthorizationCodeAsync(factory, code);

        using var body = await AssertDefaultTokenBodyAsync(context);
        var root = body.RootElement;
        var expectedFields = new[]
        {
            "access_token", "expires_in", "refresh_token", "scope", "token_type", "AccountKeys",
            "ForcePasswordReset", "Kdf", "KdfIterations", "KdfMemory", "KdfParallelism", "Key",
            "MasterPasswordPolicy", "PrivateKey", "UserDecryptionOptions",
        };
        Assert.Equal(expectedFields.Order(StringComparer.Ordinal),
            root.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        AssertRefreshTokenExists(root);
        Assert.Equal(user.Key, AssertHelper.AssertJsonProperty(root, "Key", JsonValueKind.String).GetString());
        Assert.Equal(user.PrivateKey,
            AssertHelper.AssertJsonProperty(root, "PrivateKey", JsonValueKind.String).GetString());
        AssertHelper.AssertJsonProperty(root, "ForcePasswordReset", JsonValueKind.False);
        Assert.Equal((int)KdfType.PBKDF2_SHA256,
            AssertHelper.AssertJsonProperty(root, "Kdf", JsonValueKind.Number).GetInt32());
        Assert.Equal(KdfConstants.PBKDF2_ITERATIONS.Default,
            AssertHelper.AssertJsonProperty(root, "KdfIterations", JsonValueKind.Number).GetInt32());
    }

    /// <summary>
    /// An SSO login logs a login event for the user.
    /// </summary>
    [Fact]
    public async Task AuthorizationCodeGrant_LogsLoginEvent()
    {
        using var factory = new IdentityApplicationFactory();
        var (user, code) = await RegisterSsoUserWithAuthorizationCodeAsync(factory);
        Assert.Equal(0, await CountUserEventsAsync(factory, user.Id, EventType.User_LoggedIn));

        var context = await PostAuthorizationCodeAsync(factory, code);

        using var body = await AssertDefaultTokenBodyAsync(context);
        Assert.Equal(1, await CountUserEventsAsync(factory, user.Id, EventType.User_LoggedIn));
    }

    /// <summary>
    /// An SSO login resets the failed login count of the user to 0.
    /// </summary>
    [Fact]
    public async Task AuthorizationCodeGrant_ResetsFailedLoginCount()
    {
        using var factory = new IdentityApplicationFactory();
        var (user, code) = await RegisterSsoUserWithAuthorizationCodeAsync(factory);
        await UpdateUserAsync(factory, user.Id, u => u.FailedLoginCount = 2);

        var context = await PostAuthorizationCodeAsync(factory, code);

        using var body = await AssertDefaultTokenBodyAsync(context);
        var userAfter = await factory.Services.GetRequiredService<IUserRepository>().GetByIdAsync(user.Id);
        Assert.Equal(0, userAfter!.FailedLoginCount);
    }

    /// <summary>
    /// An SSO login from a device that the user has not used before saves the device to the user.
    /// </summary>
    [Fact]
    public async Task AuthorizationCodeGrant_NewDevice_IsSavedToUser()
    {
        using var factory = new IdentityApplicationFactory();
        var (user, code) = await RegisterSsoUserWithAuthorizationCodeAsync(factory);
        var deviceRepository = factory.Services.GetRequiredService<IDeviceRepository>();
        Assert.Empty(await deviceRepository.GetManyByUserIdAsync(user.Id));

        var context = await PostAuthorizationCodeAsync(factory, code,
            deviceIdentifier: "new-device-identifier", deviceType: DeviceType.Android, deviceName: "android");

        using var body = await AssertDefaultTokenBodyAsync(context);
        var device = Assert.Single(await deviceRepository.GetManyByUserIdAsync(user.Id));
        Assert.Equal("new-device-identifier", device.Identifier);
        Assert.Equal(DeviceType.Android, device.Type);
        Assert.Equal("android", device.Name);
    }
}
