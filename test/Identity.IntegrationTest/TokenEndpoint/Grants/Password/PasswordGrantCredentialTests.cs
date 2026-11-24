using System.Text.Json;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Identity.IntegrationTest.Helpers;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.Helpers;
using Xunit;
using static Bit.Identity.IntegrationTest.Helpers.PasswordGrantTestHelpers;
using static Bit.Identity.IntegrationTest.Helpers.RefreshTokenGrantTestHelpers;
using static Bit.Identity.IntegrationTest.Helpers.TokenResponseAssertions;
using static Bit.Identity.IntegrationTest.Helpers.UserTestHelpers;

namespace Bit.Identity.IntegrationTest.TokenEndpoint.Grants.Password;

public class PasswordGrantCredentialTests
{
    private const string IncorrectCredentialsMessage = "Username or password is incorrect. Try again.";

    /// <summary>
    /// The login access token names the device the user logged in on, and a refreshed access token keeps it. API
    /// endpoints read the requesting device from these claims.
    /// </summary>
    [Fact]
    public async Task ValidateAsync_DeviceClaims_PersistThroughRefresh()
    {
        using var factory = new IdentityApplicationFactory();
        var (_, loginAccessToken, refreshToken) = await RegisterAndLoginWithPasswordAsync(factory);
        AccessTokenAssertions.AssertDeviceClaims(
            loginAccessToken, IdentityApplicationFactory.DefaultDeviceIdentifier, DefaultDeviceType);

        var refreshContext = await PostRefreshTokenAsync(factory, refreshToken);

        using var refreshBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(refreshContext);
        var refreshedAccessToken = AssertHelper.AssertJsonProperty(
            refreshBody.RootElement, "access_token", JsonValueKind.String).GetString()!;
        AccessTokenAssertions.AssertDeviceClaims(
            refreshedAccessToken, IdentityApplicationFactory.DefaultDeviceIdentifier, DefaultDeviceType);
    }

    /// <summary>
    /// A password login that names no device is rejected, so no access token is ever issued without device claims.
    /// </summary>
    [Fact]
    public async Task ValidateAsync_WithoutDevice_IsRejected()
    {
        using var factory = new IdentityApplicationFactory();
        await RegisterUserAsync(factory);

        var context = await factory.ContextFromPasswordAsync(DefaultUsername, DefaultPassword, includeDevice: false);

        await AssertErrorModelAsync(context, "no device information provided", expectedError: "device_error");
    }

    /// <summary>
    /// Each incorrect credential gets the same error, so the response does not show whether an account exists for the
    /// email.
    /// </summary>
    [Theory]
    [InlineData("unknown@email.qa", DefaultPassword)]
    [InlineData("unknown_username", DefaultPassword)]
    [InlineData(DefaultUsername, "wrong_master_password_hash")]
    [InlineData(DefaultUsername, "")]
    public async Task PasswordGrant_IncorrectCredentials_AreRejected(string username, string password)
    {
        using var factory = new IdentityApplicationFactory();
        await RegisterUserAsync(factory);

        var context = await factory.ContextFromPasswordAsync(username, password);

        await AssertErrorModelAsync(context, IncorrectCredentialsMessage);
    }

    /// <summary>
    /// The email match ignores letter case.
    /// </summary>
    [Fact]
    public async Task PasswordGrant_EmailInOtherLetterCase_Succeeds()
    {
        using var factory = new IdentityApplicationFactory();
        await RegisterUserAsync(factory);

        var context = await factory.ContextFromPasswordAsync(DefaultUsername.ToUpperInvariant(), DefaultPassword);

        using var body = await AssertDefaultTokenBodyAsync(context);
    }

    /// <summary>
    /// A user without a master password, such as a user who logs in with SSO and trusted devices, cannot log in with
    /// a password. The test removes the master password from a registered user and does not create the user through
    /// SSO, so it covers only the password check.
    /// </summary>
    [Fact]
    public async Task PasswordGrant_UserWithoutMasterPassword_IsRejected()
    {
        using var factory = new IdentityApplicationFactory();
        var user = await RegisterUserAsync(factory);
        Assert.NotNull(user.MasterPassword);
        await UpdateUserAsync(factory, user.Id, u => u.MasterPassword = null);

        var context = await factory.ContextFromPasswordAsync(DefaultUsername, DefaultPassword);

        await AssertErrorModelAsync(context, IncorrectCredentialsMessage);
    }

    /// <summary>
    /// A wrong password adds one to the failed login count of the user and sets the date of the last failed login.
    /// </summary>
    [Fact]
    public async Task PasswordGrant_WrongPassword_RecordsFailedLogin()
    {
        using var factory = new IdentityApplicationFactory();
        var user = await RegisterUserAsync(factory);
        Assert.Equal(0, user.FailedLoginCount);
        Assert.Null(user.LastFailedLoginDate);

        var context = await factory.ContextFromPasswordAsync(DefaultUsername, "wrong_master_password_hash");

        await AssertErrorModelAsync(context, IncorrectCredentialsMessage);
        var userAfter = await factory.Services.GetRequiredService<IUserRepository>().GetByIdAsync(user.Id);
        Assert.Equal(1, userAfter!.FailedLoginCount);
        Assert.NotNull(userAfter.LastFailedLoginDate);
    }

    /// <summary>
    /// A wrong password logs a failed login event for the user.
    /// </summary>
    [Fact]
    public async Task PasswordGrant_WrongPassword_LogsFailedLoginEvent()
    {
        using var factory = new IdentityApplicationFactory();
        var user = await RegisterUserAsync(factory);
        Assert.Equal(0, await CountUserEventsAsync(factory, user.Id, EventType.User_FailedLogIn));

        var context = await factory.ContextFromPasswordAsync(DefaultUsername, "wrong_master_password_hash");

        await AssertErrorModelAsync(context, IncorrectCredentialsMessage);
        Assert.Equal(1, await CountUserEventsAsync(factory, user.Id, EventType.User_FailedLogIn));
    }

    /// <summary>
    /// A request without a username is rejected before the credentials are checked.
    /// </summary>
    [Fact]
    public async Task PasswordGrant_MissingUsername_IsRejected()
    {
        using var factory = new IdentityApplicationFactory();
        await RegisterUserAsync(factory);

        var context = await factory.ContextFromPasswordAsync("", DefaultPassword);

        await AssertInvalidGrantAsync(context);
    }

    /// <summary>
    /// The Send client cannot use the password grant.
    /// </summary>
    [Fact]
    public async Task PasswordGrant_SendClient_IsRejected()
    {
        using var factory = new IdentityApplicationFactory();
        await RegisterUserAsync(factory);

        var context = await factory.ContextFromPasswordAsync(DefaultUsername, DefaultPassword,
            clientId: BitwardenClient.Send);

        await AssertErrorAsync(context, "unauthorized_client");
    }
}
