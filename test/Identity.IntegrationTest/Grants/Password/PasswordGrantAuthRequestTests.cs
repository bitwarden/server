using System.Text.Json;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.Helpers;
using Microsoft.AspNetCore.Identity;
using Xunit;
using static Bit.Identity.IntegrationTest.Helpers.PasswordGrantTestHelpers;
using static Bit.Identity.IntegrationTest.Helpers.TokenEndpointTestHelpers;

namespace Bit.Identity.IntegrationTest.Grants.Password;

public class PasswordGrantAuthRequestTests
{
    [Fact]
    public async Task ValidateAsync_ValidateContextAsync_ValidAuthRequest_Success()
    {
        // Arrange
        var localFactory = new IdentityApplicationFactory();

        // Ensure User
        await RegisterUserAsync(localFactory);
        var userManager = localFactory.GetService<UserManager<User>>();
        var user = await userManager.FindByEmailAsync(DefaultUsername);
        Assert.NotNull(user);

        // Ensure device is known b/c auth requests aren't allowed for unknown devices.
        await AddKnownDevice(localFactory, user.Id);

        // Create valid auth request and tie it to the user
        var authRequest = CreateAuthRequest(r =>
        {
            r.ResponseDate = DateTime.UtcNow.AddMinutes(-2); // 2 minutes ago
            r.Approved = true; // approved
            r.CreationDate = DateTime.UtcNow.AddMinutes(-5); // not expired
            r.Type = AuthRequestType.AuthenticateAndUnlock; // authN request
            r.AuthenticationDate = null; // not used for authN yet
            r.UserId = user.Id;  // connect request to user
            r.AccessCode = DefaultPassword; // matches the password
        });

        var authRequestRepository = localFactory.GetService<IAuthRequestRepository>();
        await authRequestRepository.CreateAsync(authRequest);

        var expectedAuthRequest = await authRequestRepository.GetManyByUserIdAsync(user.Id);
        Assert.NotEmpty(expectedAuthRequest);

        // Act
        var context = await localFactory.Server.PostAsync("/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "scope", "api offline_access" },
                { "client_id", "web" },
                { "deviceType", DeviceTypeAsString(DefaultDeviceType) },
                { "deviceIdentifier", DefaultDeviceIdentifier },
                { "deviceName", DefaultDeviceName },
                { "grant_type", "password" },
                { "username", DefaultUsername },
                { "password", DefaultPassword },
                { "AuthRequest", authRequest.Id.ToString().ToLowerInvariant() }
            }));

        // Assert
        var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        var token = AssertHelper.AssertJsonProperty(root, "access_token", JsonValueKind.String).GetString();
        Assert.NotNull(token);
    }

    [Fact]
    public async Task ValidateAsync_ValidateContextAsync_ValidAuthRequest_UnknownDevice_Failure()
    {
        // Arrange
        var localFactory = new IdentityApplicationFactory();

        // Ensure User
        await RegisterUserAsync(localFactory);
        var userManager = localFactory.GetService<UserManager<User>>();
        var user = await userManager.FindByEmailAsync(DefaultUsername);
        Assert.NotNull(user);

        // Create valid auth request and tie it to the user
        var authRequest = CreateAuthRequest(r =>
        {
            r.ResponseDate = DateTime.UtcNow.AddMinutes(-2); // 2 minutes ago
            r.Approved = true; // approved
            r.CreationDate = DateTime.UtcNow.AddMinutes(-5); // not expired
            r.Type = AuthRequestType.AuthenticateAndUnlock; // authN request
            r.AuthenticationDate = null; // not used for authN yet
            r.UserId = user.Id;  // connect request to user
            r.AccessCode = DefaultPassword; // matches the password
        });

        var authRequestRepository = localFactory.GetService<IAuthRequestRepository>();
        await authRequestRepository.CreateAsync(authRequest);

        var expectedAuthRequest = await authRequestRepository.GetManyByUserIdAsync(user.Id);
        Assert.NotEmpty(expectedAuthRequest);

        // Act
        var context = await localFactory.Server.PostAsync("/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "scope", "api offline_access" },
                { "client_id", "web" },
                { "deviceType", DeviceTypeAsString(DefaultDeviceType) },
                { "deviceIdentifier", DefaultDeviceIdentifier },
                { "deviceName", DefaultDeviceName },
                { "grant_type", "password" },
                { "username", DefaultUsername },
                { "password", DefaultPassword },
                { "AuthRequest", authRequest.Id.ToString().ToLowerInvariant() }
            }));

        // Assert
        var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        var errorModel = AssertHelper.AssertJsonProperty(root, "ErrorModel", JsonValueKind.Object);
        var errorMessage = AssertHelper.AssertJsonProperty(errorModel, "Message", JsonValueKind.String).GetString();
        Assert.Equal("auth request flow unsupported on unknown device", errorMessage);
    }

    [Fact]
    public async Task ValidateAsync_ValidateContextAsync_Expired_AuthRequest_Failure()
    {
        // Arrange
        var localFactory = new IdentityApplicationFactory();
        // Ensure User
        await RegisterUserAsync(localFactory);
        var userManager = localFactory.GetService<UserManager<User>>();

        var user = await userManager.FindByEmailAsync(DefaultUsername);
        Assert.NotNull(user);

        // Ensure device is known b/c auth requests aren't allowed for unknown devices.
        await AddKnownDevice(localFactory, user.Id);

        // Create AuthRequest
        var authRequest = CreateAuthRequest(r =>
        {
            r.ResponseDate = DateTime.UtcNow.AddMinutes(-10); // 10 minutes ago
            r.Approved = true; // approved
            r.CreationDate = DateTime.UtcNow.AddMinutes(-16); // expired after 15 minutes
            r.Type = AuthRequestType.AuthenticateAndUnlock; // authN request
            r.AuthenticationDate = null; // not used for authN yet
            r.UserId = user.Id;  // connect request to user
            r.AccessCode = DefaultPassword; // matches the password
        });

        var authRequestRepository = localFactory.GetService<IAuthRequestRepository>();
        await authRequestRepository.CreateAsync(authRequest);

        var expectedAuthRequest = await authRequestRepository.GetManyByUserIdAsync(user.Id);
        Assert.NotEmpty(expectedAuthRequest);

        // Act
        var context = await localFactory.Server.PostAsync("/connect/token",
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
            }));

        // Assert

        await AssertStandardError(context);
    }

    [Fact]
    public async Task ValidateAsync_ValidateContextAsync_Unapproved_AuthRequest_Failure()
    {
        // Arrange
        var localFactory = new IdentityApplicationFactory();
        // Ensure User
        await RegisterUserAsync(localFactory);
        var userManager = localFactory.GetService<UserManager<User>>();

        var user = await userManager.FindByEmailAsync(DefaultUsername);
        Assert.NotNull(user);

        // Ensure device is known b/c auth requests aren't allowed for unknown devices.
        await AddKnownDevice(localFactory, user.Id);

        // Create AuthRequest
        var authRequest = CreateAuthRequest(r =>
        {
            r.ResponseDate = DateTime.UtcNow.AddMinutes(-2); // 2 minutes ago
            r.Approved = false; // NOT approved
            r.CreationDate = DateTime.UtcNow.AddMinutes(-5); // still valid
            r.Type = AuthRequestType.AuthenticateAndUnlock; // authN request
            r.AuthenticationDate = null; // not used for authN yet
            r.UserId = user.Id;  // connect request to user
            r.AccessCode = DefaultPassword; // matches the password
        });

        var authRequestRepository = localFactory.GetService<IAuthRequestRepository>();
        await authRequestRepository.CreateAsync(authRequest);

        var expectedAuthRequest = await authRequestRepository.GetManyByUserIdAsync(user.Id);
        Assert.NotEmpty(expectedAuthRequest);

        // Act
        var context = await localFactory.Server.PostAsync("/connect/token",
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
            }));

        // Assert

        await AssertStandardError(context);
    }

    public static IEnumerable<object[]> InvalidAuthRequestTypes()
    {
        // yield the two enum values that should fail
        yield return [AuthRequestType.Unlock];
        yield return [AuthRequestType.AdminApproval];
    }

    [Theory]
    [MemberData(nameof(InvalidAuthRequestTypes))]
    public async Task ValidateAsync_ValidateContextAsync_AuthRequest_Invalid_Type_Failure(AuthRequestType invalidType)
    {
        // Arrange
        var localFactory = new IdentityApplicationFactory();
        // Ensure User
        await RegisterUserAsync(localFactory);
        var userManager = localFactory.GetService<UserManager<User>>();

        var user = await userManager.FindByEmailAsync(DefaultUsername);
        Assert.NotNull(user);

        // Ensure device is known b/c auth requests aren't allowed for unknown devices.
        await AddKnownDevice(localFactory, user.Id);

        // Create AuthRequest
        var authRequest = CreateAuthRequest(r =>
        {
            r.ResponseDate = DateTime.UtcNow.AddMinutes(-2); // 2 minutes ago
            r.Approved = true; // approved
            r.CreationDate = DateTime.UtcNow.AddMinutes(-5); // still valid
            r.Type = invalidType; // invalid type for authN
            r.AuthenticationDate = null; // not used for authN yet
            r.UserId = user.Id;  // connect request to user
            r.AccessCode = DefaultPassword; // matches the password
        });

        var authRequestRepository = localFactory.GetService<IAuthRequestRepository>();
        await authRequestRepository.CreateAsync(authRequest);

        var expectedAuthRequest = await authRequestRepository.GetManyByUserIdAsync(user.Id);
        Assert.NotEmpty(expectedAuthRequest);

        // Act
        var context = await localFactory.Server.PostAsync("/connect/token",
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
            }));

        // Assert

        await AssertStandardError(context);
    }

    [Fact]
    public async Task ValidateAsync_ValidateContextAsync_AuthRequest_WrongUser_Failure()
    {
        // Arrange
        var localFactory = new IdentityApplicationFactory();

        // Ensure User 1 exists
        await RegisterUserAsync(localFactory);
        var userManager = localFactory.GetService<UserManager<User>>();
        var user = await userManager.FindByEmailAsync(DefaultUsername);
        Assert.NotNull(user);



        // Ensure User 2 exists so we can satisfy auth request foreign key constraint
        var user2Username = "user2@email.com";
        var user2Password = "user2_password";
        await RegisterUserAsync(localFactory, user2Username, user2Password);
        var user2 = await userManager.FindByEmailAsync(user2Username);
        Assert.NotNull(user2);

        // Ensure device is known b/c auth requests aren't allowed for unknown devices.
        await AddKnownDevice(localFactory, user.Id);

        // Create valid auth request for user 2
        var authRequest = CreateAuthRequest(r =>
        {
            r.ResponseDate = DateTime.UtcNow.AddMinutes(-2); // 2 minutes ago
            r.Approved = true; // approved
            r.CreationDate = DateTime.UtcNow.AddMinutes(-5); // not expired
            r.Type = AuthRequestType.AuthenticateAndUnlock; // authN request
            r.AuthenticationDate = null; // not used for authN yet
            r.UserId = user2.Id;  // connect request to user2
            r.AccessCode = DefaultPassword; // matches the password
        });
        var authRequestRepository = localFactory.GetService<IAuthRequestRepository>();
        await authRequestRepository.CreateAsync(authRequest);

        var expectedAuthRequest = await authRequestRepository.GetManyByUserIdAsync(user2.Id);
        Assert.NotEmpty(expectedAuthRequest);

        // Act
        var context = await localFactory.Server.PostAsync("/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "scope", "api offline_access" },
                { "client_id", "web" },
                { "deviceType", DeviceTypeAsString(DefaultDeviceType) },
                { "deviceIdentifier", DefaultDeviceIdentifier },
                { "deviceName", DefaultDeviceName },
                { "grant_type", "password" },
                { "username", DefaultUsername },
                { "password", DefaultPassword },
                { "AuthRequest", authRequest.Id.ToString().ToLowerInvariant() }
            }));

        // Assert
        var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        var errorModel = AssertHelper.AssertJsonProperty(root, "ErrorModel", JsonValueKind.Object);
        var errorMessage = AssertHelper.AssertJsonProperty(errorModel, "Message", JsonValueKind.String).GetString();
        Assert.Equal("Username or password is incorrect. Try again.", errorMessage);
    }

    [Fact]
    public async Task ValidateAsync_ValidateContextAsync_Unanswered_AuthRequest_Failure()
    {
        // Arrange
        var localFactory = new IdentityApplicationFactory();
        // Ensure User
        await RegisterUserAsync(localFactory);
        var userManager = localFactory.GetService<UserManager<User>>();

        var user = await userManager.FindByEmailAsync(DefaultUsername);
        Assert.NotNull(user);

        // Ensure device is known b/c auth requests aren't allowed for unknown devices.
        await AddKnownDevice(localFactory, user.Id);

        // Create AuthRequest
        var authRequest = CreateAuthRequest(r =>
        {
            r.ResponseDate = null; // not answered
            r.Approved = true; // approved
            r.CreationDate = DateTime.UtcNow.AddMinutes(-5); // still valid
            r.Type = AuthRequestType.AuthenticateAndUnlock; // authN request
            r.AuthenticationDate = null; // not used for authN yet
            r.UserId = user.Id;  // connect request to user
            r.AccessCode = DefaultPassword; // matches the password
        });

        var authRequestRepository = localFactory.GetService<IAuthRequestRepository>();
        await authRequestRepository.CreateAsync(authRequest);

        var expectedAuthRequest = await authRequestRepository.GetManyByUserIdAsync(user.Id);
        Assert.NotEmpty(expectedAuthRequest);

        // Act
        var context = await localFactory.Server.PostAsync("/connect/token",
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
            }));

        // Assert

        await AssertStandardError(context);
    }

    [Fact]
    public async Task ValidateAsync_ValidateContextAsync_WrongPassword_AuthRequest_Failure()
    {
        // Arrange
        var localFactory = new IdentityApplicationFactory();
        // Ensure User
        await RegisterUserAsync(localFactory);
        var userManager = localFactory.GetService<UserManager<User>>();

        var user = await userManager.FindByEmailAsync(DefaultUsername);
        Assert.NotNull(user);

        // Ensure device is known b/c auth requests aren't allowed for unknown devices.
        await AddKnownDevice(localFactory, user.Id);

        // Create AuthRequest
        var authRequest = CreateAuthRequest(r =>
        {
            r.ResponseDate = DateTime.UtcNow.AddMinutes(-2); // answered
            r.Approved = true; // approved
            r.CreationDate = DateTime.UtcNow.AddMinutes(-5); // still valid
            r.Type = AuthRequestType.AuthenticateAndUnlock; // authN request
            r.AuthenticationDate = null; // not used for authN yet
            r.UserId = user.Id;  // connect request to user
            r.AccessCode = "WRONG_BAD_PASSWORD"; // does not match the password
        });

        var authRequestRepository = localFactory.GetService<IAuthRequestRepository>();
        await authRequestRepository.CreateAsync(authRequest);

        var expectedAuthRequest = await authRequestRepository.GetManyByUserIdAsync(user.Id);
        Assert.NotEmpty(expectedAuthRequest);

        // Act
        var context = await localFactory.Server.PostAsync("/connect/token",
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
            }));

        // Assert

        await AssertStandardError(context);
    }

    [Fact]
    public async Task ValidateAsync_ValidateContextAsync_Spent_AuthRequest_Failure()
    {
        // Arrange
        var localFactory = new IdentityApplicationFactory();
        // Ensure User
        await RegisterUserAsync(localFactory);
        var userManager = localFactory.GetService<UserManager<User>>();

        var user = await userManager.FindByEmailAsync(DefaultUsername);
        Assert.NotNull(user);

        // Ensure device is known b/c auth requests aren't allowed for unknown devices.
        await AddKnownDevice(localFactory, user.Id);

        // Create AuthRequest
        var authRequest = CreateAuthRequest(r =>
        {
            r.ResponseDate = DateTime.UtcNow.AddMinutes(-2); // answered
            r.Approved = true; // approved
            r.CreationDate = DateTime.UtcNow.AddMinutes(-5); // still valid
            r.Type = AuthRequestType.AuthenticateAndUnlock; // authN request
            r.AuthenticationDate = DateTime.UtcNow.AddMinutes(-2); // spent request - already has been used for authN
            r.UserId = user.Id;  // connect request to user
            r.AccessCode = DefaultPassword; // does not match the password
        });

        var authRequestRepository = localFactory.GetService<IAuthRequestRepository>();
        await authRequestRepository.CreateAsync(authRequest);

        var expectedAuthRequest = await authRequestRepository.GetManyByUserIdAsync(user.Id);
        Assert.NotEmpty(expectedAuthRequest);

        // Act
        var context = await localFactory.Server.PostAsync("/connect/token",
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
            }));

        // Assert
        await AssertStandardError(context);
    }

    private AuthRequest CreateAuthRequest(Action<AuthRequest>? customize = null)
    {
        var req = new AuthRequest
        {
            // required fields with defaults
            UserId = Guid.NewGuid(),
            Type = AuthRequestType.AuthenticateAndUnlock,
            RequestDeviceIdentifier = DefaultDeviceIdentifier,
            RequestIpAddress = "1.1.1.1",
            AccessCode = DefaultPassword,
            PublicKey = "test_public_key",
            CreationDate = DateTime.UtcNow,
        };

        // let the caller tweak whatever they need
        customize?.Invoke(req);

        return req;
    }

    private async Task AddKnownDevice(IdentityApplicationFactory factory, Guid userId)
    {
        var userDevice = new Device
        {
            Identifier = DefaultDeviceIdentifier,
            Type = DefaultDeviceType,
            Name = DefaultDeviceName,
            UserId = userId,
        };
        var deviceRepository = factory.GetService<IDeviceRepository>();
        await deviceRepository.CreateAsync(userDevice);
    }

    private async Task AssertStandardError(HttpContext context)
    {
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
}
