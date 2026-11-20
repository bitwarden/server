using System.Diagnostics;
using System.Text.Json;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Identity.IntegrationTest.Helpers;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Microsoft.AspNetCore.Identity;
using Xunit;
using static Bit.Identity.IntegrationTest.Helpers.PasswordGrantTestHelpers;
using static Bit.Identity.IntegrationTest.Helpers.TokenEndpointTestHelpers;
using static Bit.Identity.IntegrationTest.Helpers.TokenResponseAssertions;
using static Bit.Identity.IntegrationTest.Helpers.UserTestHelpers;

namespace Bit.Identity.IntegrationTest.TokenEndpoint.Grants.Password;

public class PasswordGrantCredentialTests
{
    private const string IncorrectCredentialsMessage = "Username or password is incorrect. Try again.";

    [Fact]
    public async Task ValidateAsync_Success()
    {
        // Arrange
        var localFactory = new IdentityApplicationFactory();
        await RegisterUserAsync(localFactory);

        // Act
        var context = await localFactory.Server.PostAsync("/connect/token",
            GetFormUrlEncodedContent());

        // Assert
        var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        var token = AssertHelper.AssertJsonProperty(root, "access_token", JsonValueKind.String).GetString();
        Assert.NotNull(token);
    }

    /// <summary>
    /// The login access token names the device the user logged in on, and a refreshed access token keeps it. API
    /// endpoints read the requesting device from these claims.
    /// </summary>
    [Fact]
    public async Task ValidateAsync_DeviceClaims_PersistThroughRefresh()
    {
        // Arrange
        var localFactory = new IdentityApplicationFactory();
        await RegisterUserAsync(localFactory);
        var loginContext = await localFactory.Server.PostAsync("/connect/token", GetFormUrlEncodedContent());
        using var loginBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(loginContext);
        var loginAccessToken = AssertHelper.AssertJsonProperty(
            loginBody.RootElement, "access_token", JsonValueKind.String).GetString()!;
        var refreshToken = AssertHelper.AssertJsonProperty(
            loginBody.RootElement, "refresh_token", JsonValueKind.String).GetString()!;
        AccessTokenAssertions.AssertDeviceClaims(loginAccessToken, DefaultDeviceIdentifier, DefaultDeviceType);

        // Act
        using var refreshContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "refresh_token" },
            { "client_id", "web" },
            { "refresh_token", refreshToken },
        });
        var refreshContext = await localFactory.Server.PostAsync("/connect/token", refreshContent);

        // Assert
        using var refreshBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(refreshContext);
        var refreshedAccessToken = AssertHelper.AssertJsonProperty(
            refreshBody.RootElement, "access_token", JsonValueKind.String).GetString()!;
        AccessTokenAssertions.AssertDeviceClaims(refreshedAccessToken, DefaultDeviceIdentifier, DefaultDeviceType);
    }

    /// <summary>
    /// A password login that names no device is rejected, so no access token is ever issued without device claims.
    /// </summary>
    [Fact]
    public async Task ValidateAsync_WithoutDevice_IsRejected()
    {
        // Arrange
        var localFactory = new IdentityApplicationFactory();
        await RegisterUserAsync(localFactory);

        // Act
        var context = await localFactory.Server.PostAsync("/connect/token",
            GetDefaultFormUrlEncodedContentWithoutDevice());

        // Assert
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        Assert.False(body.RootElement.TryGetProperty("access_token", out _));
        var errorModel = AssertHelper.AssertJsonProperty(body.RootElement, "ErrorModel", JsonValueKind.Object);
        Assert.Equal("no device information provided",
            AssertHelper.AssertJsonProperty(errorModel, "Message", JsonValueKind.String).GetString());
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_UserNull_Failure(string username)
    {
        // Arrange
        var localFactory = new IdentityApplicationFactory();
        // Act
        var context = await localFactory.Server.PostAsync("/connect/token",
            GetFormUrlEncodedContent(username: username));

        // Assert
        var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        var errorModel = AssertHelper.AssertJsonProperty(root, "ErrorModel", JsonValueKind.Object);
        var errorMessage = AssertHelper.AssertJsonProperty(errorModel, "Message", JsonValueKind.String).GetString();
        Assert.Equal("Username or password is incorrect. Try again.", errorMessage);
    }

    /// <summary>
    /// I would have liked to spy into the IUserService but by spying into the IUserService it
    /// creates a Singleton that is not available to the UserManager<User> thus causing the
    /// RegisterAsync() to create a the user in a different UserStore than the one the
    /// UserManager<User> has access to (This is an assumption made from observing the behavior while
    /// writing theses tests, I could be wrong).
    ///
    /// For the time being, verifying that the user is not null confirms that the failure is due to
    /// a bad password.
    /// </summary>
    /// <param name="badPassword">random password</param>
    /// <returns></returns>
    [Theory, BitAutoData]
    public async Task ValidateAsync_BadPassword_Failure(string badPassword)
    {
        // Arrange
        var localFactory = new IdentityApplicationFactory();
        await RegisterUserAsync(localFactory);

        var userManager = localFactory.GetService<UserManager<User>>();

        // Verify the User is not null to ensure the failure is due to bad password
        Assert.NotNull(await userManager.FindByEmailAsync(DefaultUsername));

        // Act
        var context = await localFactory.Server.PostAsync("/connect/token",
            GetFormUrlEncodedContent(password: badPassword));

        // Assert
        var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        var errorModel = AssertHelper.AssertJsonProperty(root, "ErrorModel", JsonValueKind.Object);
        var errorMessage = AssertHelper.AssertJsonProperty(errorModel, "Message", JsonValueKind.String).GetString();
        Assert.Equal("Username or password is incorrect. Try again.", errorMessage);
    }

    /// <summary>
    /// Each incorrect credential gets the same error, so the response does not show whether an account exists for the
    /// email.
    /// </summary>
    [Theory]
    [InlineData("unknown@email.qa", DefaultPassword)]
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
    /// a password.
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
    /// A failed login waits at least 2 seconds before it responds, which slows down password guessing.
    /// </summary>
    [Fact]
    public async Task PasswordGrant_WrongPassword_RespondsAfterDelay()
    {
        using var factory = new IdentityApplicationFactory();
        await RegisterUserAsync(factory);

        var stopwatch = Stopwatch.StartNew();
        var context = await factory.ContextFromPasswordAsync(DefaultUsername, "wrong_master_password_hash");
        stopwatch.Stop();

        await AssertErrorModelAsync(context, IncorrectCredentialsMessage);
        Assert.True(stopwatch.Elapsed >= TimeSpan.FromSeconds(2), $"Responded after {stopwatch.Elapsed}.");
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

    private FormUrlEncodedContent GetFormUrlEncodedContent(
        string deviceId = null, string username = null, string password = null)
    {
        return new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "scope", "api offline_access" },
            { "client_id", "web" },
            { "deviceType", DeviceTypeAsString(DeviceType.FirefoxBrowser) },
            { "deviceIdentifier", deviceId ?? DefaultDeviceIdentifier },
            { "deviceName", "firefox" },
            { "grant_type", "password" },
            { "username", username ?? DefaultUsername },
            { "password", password ?? DefaultPassword },
        });
    }

    private FormUrlEncodedContent GetDefaultFormUrlEncodedContentWithoutDevice()
    {
        return new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "scope", "api offline_access" },
            { "client_id", "web" },
            { "grant_type", "password" },
            { "username", DefaultUsername },
            { "password", DefaultPassword },
        });
    }
}
