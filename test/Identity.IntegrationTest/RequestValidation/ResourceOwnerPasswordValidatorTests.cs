using System.Text.Json;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Identity.IdentityServer.RequestValidators;
using Bit.Identity.Models.Request.Accounts;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Xunit;

namespace Bit.Identity.IntegrationTest.RequestValidation;

public class ResourceOwnerPasswordValidatorTests : IClassFixture<IdentityApplicationFactory>
{
    private const string DefaultPassword = "master_password_hash";
    private const string DefaultUsername = "test@email.qa";
    private const string DefaultDeviceIdentifier = "test_identifier";
    private readonly IdentityApplicationFactory _factory;
    private readonly UserManager<User> _userManager;
    private readonly IAuthRequestRepository _authRequestRepository;
    private readonly IDeviceService _deviceService;

    public ResourceOwnerPasswordValidatorTests(IdentityApplicationFactory factory)
    {
        _factory = factory;

        _userManager = _factory.GetService<UserManager<User>>();
        _authRequestRepository = _factory.GetService<IAuthRequestRepository>();
        _deviceService = _factory.GetService<IDeviceService>();
    }

    [Fact]
    public async Task ValidateAsync_Success()
    {
        // Arrange
        await EnsureUserCreatedAsync();

        // Act
        var context = await _factory.Server.PostAsync("/connect/token",
            GetFormUrlEncodedContent(),
            context => context.SetAuthEmail(DefaultUsername));

        // Assert
        var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        var token = AssertHelper.AssertJsonProperty(root, "access_token", JsonValueKind.String).GetString();
        Assert.NotNull(token);
    }

    [Fact]
    public async Task ValidateAsync_AuthEmailHeaderInvalid_InvalidGrantResponse()
    {
        // Arrange
        await EnsureUserCreatedAsync();

        // Act
        var context = await _factory.Server.PostAsync(
            "/connect/token",
            GetFormUrlEncodedContent()
        );

        // Assert
        var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        var error = AssertHelper.AssertJsonProperty(root, "error_description", JsonValueKind.String).GetString();
        Assert.Equal("Auth-Email header invalid.", error);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_UserNull_Failure(string username)
    {
        // Act
        var context = await _factory.Server.PostAsync("/connect/token",
            GetFormUrlEncodedContent(username: username),
            context => context.SetAuthEmail(username));

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
        await EnsureUserCreatedAsync();

        // Verify the User is not null to ensure the failure is due to bad password
        Assert.NotNull(await _userManager.FindByEmailAsync(DefaultUsername));

        // Act
        var context = await _factory.Server.PostAsync("/connect/token",
            GetFormUrlEncodedContent(password: badPassword),
            context => context.SetAuthEmail(DefaultUsername));

        // Assert
        var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        var errorModel = AssertHelper.AssertJsonProperty(root, "ErrorModel", JsonValueKind.Object);
        var errorMessage = AssertHelper.AssertJsonProperty(errorModel, "Message", JsonValueKind.String).GetString();
        Assert.Equal("Username or password is incorrect. Try again.", errorMessage);
    }

    [Fact]
    public async Task ValidateAsync_ValidateContextAsync_AuthRequest_NotNull_AgeLessThanOneHour_Success()
    {
        // Arrange
        // Ensure User
        await EnsureUserCreatedAsync();
        var user = await _userManager.FindByEmailAsync(DefaultUsername);
        Assert.NotNull(user);

        // Connect Request to User and set CreationDate
        var authRequest = CreateAuthRequest(
            user.Id,
            AuthRequestType.AuthenticateAndUnlock,
            DateTime.UtcNow.AddMinutes(-30)
        );
        await _authRequestRepository.CreateAsync(authRequest);

        var expectedAuthRequest = await _authRequestRepository.GetManyByUserIdAsync(user.Id);
        Assert.NotEmpty(expectedAuthRequest);

        // Act
        var context = await _factory.Server.PostAsync("/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "scope", "api offline_access" },
                { "client_id", "web" },
                { "deviceType", DeviceTypeAsString(DeviceType.FirefoxBrowser) },
                { "deviceIdentifier", DefaultDeviceIdentifier },
                { "deviceName", "firefox" },
                { "grant_type", "password" },
                { "username", DefaultUsername },
                { "password", DefaultPassword },
                { "AuthRequest", authRequest.Id.ToString().ToLowerInvariant() }
            }), context => context.SetAuthEmail(DefaultUsername));

        // Assert
        var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        var token = AssertHelper.AssertJsonProperty(root, "access_token", JsonValueKind.String).GetString();
        Assert.NotNull(token);
    }

    [Fact]
    public async Task ValidateAsync_ValidateContextAsync_AuthRequest_NotNull_AgeGreaterThanOneHour_Failure()
    {
        // Arrange
        // Ensure User
        await EnsureUserCreatedAsync(_factory);
        var user = await _userManager.FindByEmailAsync(DefaultUsername);
        Assert.NotNull(user);

        // Create AuthRequest
        var authRequest = CreateAuthRequest(
            user.Id,
            AuthRequestType.AuthenticateAndUnlock,
            DateTime.UtcNow.AddMinutes(-61)
        );

        // Act
        var context = await _factory.Server.PostAsync("/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "scope", "api offline_access" },
                { "client_id", "web" },
                { "deviceType", DeviceTypeAsString(DeviceType.FirefoxBrowser) },
                { "deviceIdentifier", DefaultDeviceIdentifier },
                { "deviceName", "firefox" },
                { "grant_type", "password" },
                { "username", DefaultUsername },
                { "password", DefaultPassword },
                { "AuthRequest", authRequest.Id.ToString().ToLowerInvariant() }
            }), context => context.SetAuthEmail(DefaultUsername));

        // Assert

        /*
        An improvement on the current failure flow would be to document which part of
        the flow failed since all of the failures are basically the same.
        This doesn't build confidence in the tests.
        */

        var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        var errorModel = AssertHelper.AssertJsonProperty(root, "ErrorModel", JsonValueKind.Object);
        var errorMessage = AssertHelper.AssertJsonProperty(errorModel, "Message", JsonValueKind.String).GetString();
        Assert.Equal("Username or password is incorrect. Try again.", errorMessage);
    }

    [Fact]
    public async Task ValidateAsync_DeviceSaveAsync_ReturnsNullDevice_ErrorResult()
    {
        // Arrange
        var factory = new IdentityApplicationFactory();

        // Stub DeviceValidator
        factory.SubstituteService<IDeviceValidator>(sub =>
        {
            sub.SaveDeviceAsync(Arg.Any<User>(), Arg.Any<ValidatedTokenRequest>())
                .Returns(null as Device);
        });

        // Add User
        await factory.RegisterAsync(new RegisterRequestModel
        {
            Email = DefaultUsername,
            MasterPasswordHash = DefaultPassword
        });
        var userManager = factory.GetService<UserManager<User>>();
        await factory.RegisterAsync(new RegisterRequestModel
        {
            Email = DefaultUsername,
            MasterPasswordHash = DefaultPassword
        });
        var user = await userManager.FindByEmailAsync(DefaultUsername);
        Assert.NotNull(user);

        // Act
        var context = await factory.Server.PostAsync("/connect/token",
            GetFormUrlEncodedContent(),
            context => context.SetAuthEmail(DefaultUsername));

        // Assert
        var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        var errorModel = AssertHelper.AssertJsonProperty(root, "ErrorModel", JsonValueKind.Object);
        var errorMessage = AssertHelper.AssertJsonProperty(errorModel, "Message", JsonValueKind.String).GetString();
        Assert.Equal("No device information provided.", errorMessage);
    }

    private async Task EnsureUserCreatedAsync(IdentityApplicationFactory factory = null)
    {
        factory ??= _factory;
        // No need to create more users than we need
        if (await _userManager.FindByEmailAsync(DefaultUsername) == null)
        {
            // Register user
            await factory.RegisterAsync(new RegisterRequestModel
            {
                Email = DefaultUsername,
                MasterPasswordHash = DefaultPassword
            });
        }
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

    private static string DeviceTypeAsString(DeviceType deviceType)
    {
        return ((int)deviceType).ToString();
    }

    private static AuthRequest CreateAuthRequest(
        Guid userId,
        AuthRequestType authRequestType,
        DateTime creationDate,
        bool? approved = null,
        DateTime? responseDate = null)
    {
        return new AuthRequest
        {
            UserId = userId,
            Type = authRequestType,
            Approved = approved,
            RequestDeviceIdentifier = DefaultDeviceIdentifier,
            RequestIpAddress = "1.1.1.1",
            AccessCode = DefaultPassword,
            PublicKey = "test_public_key",
            CreationDate = creationDate,
            ResponseDate = responseDate,
        };
    }
}
