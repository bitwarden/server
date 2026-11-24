using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.IntegrationTestCommon.Factories;
using Xunit;
using static Bit.Identity.IntegrationTest.Helpers.PasswordGrantTestHelpers;
using static Bit.Identity.IntegrationTest.Helpers.TokenEndpointTestHelpers;
using static Bit.Identity.IntegrationTest.Helpers.TokenResponseAssertions;

namespace Bit.Identity.IntegrationTest.TokenEndpoint.Grants.Password;

/// <summary>
/// Integration tests for a <c>password</c> grant that sends an approved auth request ("login with device") in place
/// of the master password. Each failure test starts from the valid auth request of the success test and changes one
/// thing.
/// </summary>
public class PasswordGrantAuthRequestTests
{
    private const string IncorrectCredentialsMessage = "Username or password is incorrect. Try again.";

    /// <summary>
    /// The access code differs from the master password, so a login succeeds only through the auth request check.
    /// </summary>
    private const string AccessCode = "auth_request_access_code";

    /// <summary>
    /// How long after its creation an auth request can log a user in.
    /// </summary>
    private static readonly TimeSpan AuthRequestLifetime = TimeSpan.FromMinutes(15);

    /// <summary>
    /// An approved, answered, unused auth request on a known device logs the user in with its access code, and the
    /// login marks the auth request as used.
    /// </summary>
    [Fact]
    public async Task ValidateAsync_ValidateContextAsync_ValidAuthRequest_Success()
    {
        using var factory = new IdentityApplicationFactory();
        var user = await RegisterUserAsync(factory);
        await AddKnownDevice(factory, user.Id);
        var authRequest = await CreateAuthRequestAsync(factory, user.Id);
        Assert.Null(authRequest.AuthenticationDate);

        var context = await PostAuthRequestLoginAsync(factory, authRequest.Id, AccessCode);

        using var body = await AssertDefaultTokenBodyAsync(context);
        var authRequestAfter = await factory.GetService<IAuthRequestRepository>().GetByIdAsync(authRequest.Id);
        Assert.NotNull(authRequestAfter!.AuthenticationDate);
    }

    /// <summary>
    /// An auth request login from a device that the user has not used before is rejected.
    /// </summary>
    [Fact]
    public async Task ValidateAsync_ValidateContextAsync_ValidAuthRequest_UnknownDevice_Failure()
    {
        using var factory = new IdentityApplicationFactory();
        var user = await RegisterUserAsync(factory);
        var authRequest = await CreateAuthRequestAsync(factory, user.Id);

        var context = await PostAuthRequestLoginAsync(factory, authRequest.Id, AccessCode);

        await AssertErrorModelAsync(context, "auth request flow unsupported on unknown device",
            expectedError: "device_error");
    }

    /// <summary>
    /// An auth request older than its lifetime is rejected.
    /// </summary>
    [Fact]
    public async Task ValidateAsync_ValidateContextAsync_Expired_AuthRequest_Failure()
    {
        using var factory = new IdentityApplicationFactory();
        var user = await RegisterUserAsync(factory);
        await AddKnownDevice(factory, user.Id);
        var authRequest = await CreateAuthRequestAsync(factory, user.Id,
            r => r.CreationDate = DateTime.UtcNow - AuthRequestLifetime - TimeSpan.FromMinutes(1));

        var context = await PostAuthRequestLoginAsync(factory, authRequest.Id, AccessCode);

        await AssertErrorModelAsync(context, IncorrectCredentialsMessage);
    }

    /// <summary>
    /// An auth request that the user denied is rejected.
    /// </summary>
    [Fact]
    public async Task ValidateAsync_ValidateContextAsync_Unapproved_AuthRequest_Failure()
    {
        using var factory = new IdentityApplicationFactory();
        var user = await RegisterUserAsync(factory);
        await AddKnownDevice(factory, user.Id);
        var authRequest = await CreateAuthRequestAsync(factory, user.Id, r => r.Approved = false);

        var context = await PostAuthRequestLoginAsync(factory, authRequest.Id, AccessCode);

        await AssertErrorModelAsync(context, IncorrectCredentialsMessage);
    }

    public static IEnumerable<object[]> InvalidAuthRequestTypes()
    {
        yield return [AuthRequestType.Unlock];
        yield return [AuthRequestType.AdminApproval];
    }

    /// <summary>
    /// Only an auth request of type <see cref="AuthRequestType.AuthenticateAndUnlock"/> can log a user in.
    /// </summary>
    [Theory]
    [MemberData(nameof(InvalidAuthRequestTypes))]
    public async Task ValidateAsync_ValidateContextAsync_AuthRequest_Invalid_Type_Failure(AuthRequestType invalidType)
    {
        using var factory = new IdentityApplicationFactory();
        var user = await RegisterUserAsync(factory);
        await AddKnownDevice(factory, user.Id);
        var authRequest = await CreateAuthRequestAsync(factory, user.Id, r => r.Type = invalidType);

        var context = await PostAuthRequestLoginAsync(factory, authRequest.Id, AccessCode);

        await AssertErrorModelAsync(context, IncorrectCredentialsMessage);
    }

    /// <summary>
    /// An auth request of another user cannot log this user in.
    /// </summary>
    [Fact]
    public async Task ValidateAsync_ValidateContextAsync_AuthRequest_WrongUser_Failure()
    {
        using var factory = new IdentityApplicationFactory();
        var user = await RegisterUserAsync(factory);
        var otherUser = await RegisterUserAsync(factory, "user2@email.com", "user2_password");
        await AddKnownDevice(factory, user.Id);
        var authRequest = await CreateAuthRequestAsync(factory, otherUser.Id);

        var context = await PostAuthRequestLoginAsync(factory, authRequest.Id, AccessCode);

        await AssertErrorModelAsync(context, IncorrectCredentialsMessage);
    }

    /// <summary>
    /// An auth request that the user has not answered yet is rejected.
    /// </summary>
    [Fact]
    public async Task ValidateAsync_ValidateContextAsync_Unanswered_AuthRequest_Failure()
    {
        using var factory = new IdentityApplicationFactory();
        var user = await RegisterUserAsync(factory);
        await AddKnownDevice(factory, user.Id);
        var authRequest = await CreateAuthRequestAsync(factory, user.Id, r => r.ResponseDate = null);

        var context = await PostAuthRequestLoginAsync(factory, authRequest.Id, AccessCode);

        await AssertErrorModelAsync(context, IncorrectCredentialsMessage);
    }

    /// <summary>
    /// With an auth request, only its access code is accepted. The master password of the user is rejected.
    /// </summary>
    [Fact]
    public async Task ValidateAsync_ValidateContextAsync_WrongPassword_AuthRequest_Failure()
    {
        using var factory = new IdentityApplicationFactory();
        var user = await RegisterUserAsync(factory);
        await AddKnownDevice(factory, user.Id);
        var authRequest = await CreateAuthRequestAsync(factory, user.Id);

        var context = await PostAuthRequestLoginAsync(factory, authRequest.Id, DefaultPassword);

        await AssertErrorModelAsync(context, IncorrectCredentialsMessage);
    }

    /// <summary>
    /// An auth request that has already logged a user in cannot log in again.
    /// </summary>
    [Fact]
    public async Task ValidateAsync_ValidateContextAsync_Spent_AuthRequest_Failure()
    {
        using var factory = new IdentityApplicationFactory();
        var user = await RegisterUserAsync(factory);
        await AddKnownDevice(factory, user.Id);
        var authRequest = await CreateAuthRequestAsync(factory, user.Id,
            r => r.AuthenticationDate = DateTime.UtcNow.AddMinutes(-2));

        var context = await PostAuthRequestLoginAsync(factory, authRequest.Id, AccessCode);

        await AssertErrorModelAsync(context, IncorrectCredentialsMessage);
    }

    /// <summary>
    /// An auth request ID that does not exist is rejected.
    /// </summary>
    [Fact]
    public async Task PasswordGrant_UnknownAuthRequestId_IsRejected()
    {
        using var factory = new IdentityApplicationFactory();
        var user = await RegisterUserAsync(factory);
        await AddKnownDevice(factory, user.Id);
        await CreateAuthRequestAsync(factory, user.Id);

        var context = await PostAuthRequestLoginAsync(factory, Guid.NewGuid(), AccessCode);

        await AssertErrorModelAsync(context, IncorrectCredentialsMessage);
    }

    /// <summary>
    /// An auth request ID that is not a GUID is rejected.
    /// </summary>
    [Fact]
    public async Task PasswordGrant_NonGuidAuthRequestId_IsRejected()
    {
        using var factory = new IdentityApplicationFactory();
        var user = await RegisterUserAsync(factory);
        await AddKnownDevice(factory, user.Id);
        await CreateAuthRequestAsync(factory, user.Id);

        var context = await PostAuthRequestLoginAsync(factory, "not_a_guid", AccessCode);

        await AssertErrorModelAsync(context, IncorrectCredentialsMessage);
    }

    /// <summary>
    /// Saves an auth request that is valid for a login of the user with <see cref="AccessCode"/>: approved,
    /// answered, not expired, and not used. <paramref name="customize"/> changes it before it is saved.
    /// </summary>
    private static async Task<AuthRequest> CreateAuthRequestAsync(IdentityApplicationFactory factory, Guid userId,
        Action<AuthRequest>? customize = null)
    {
        var authRequest = new AuthRequest
        {
            UserId = userId,
            Type = AuthRequestType.AuthenticateAndUnlock,
            RequestDeviceIdentifier = DefaultDeviceIdentifier,
            RequestIpAddress = "1.1.1.1",
            AccessCode = AccessCode,
            PublicKey = "test_public_key",
            CreationDate = DateTime.UtcNow.AddMinutes(-5),
            ResponseDate = DateTime.UtcNow.AddMinutes(-2),
            Approved = true,
            AuthenticationDate = null,
        };
        customize?.Invoke(authRequest);
        return await factory.GetService<IAuthRequestRepository>().CreateAsync(authRequest);
    }

    private static Task<HttpContext> PostAuthRequestLoginAsync(IdentityApplicationFactory factory,
        Guid authRequestId, string password) =>
        PostAuthRequestLoginAsync(factory, authRequestId.ToString(), password);

    private static async Task<HttpContext> PostAuthRequestLoginAsync(IdentityApplicationFactory factory,
        string authRequestId, string password)
    {
        return await factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "scope", "api offline_access" },
            { "client_id", "web" },
            { "deviceType", DeviceTypeAsString(DefaultDeviceType) },
            { "deviceIdentifier", DefaultDeviceIdentifier },
            { "deviceName", DefaultDeviceName },
            { "grant_type", "password" },
            { "username", DefaultUsername },
            { "password", password },
            { "AuthRequest", authRequestId },
        }));
    }

    private static async Task AddKnownDevice(IdentityApplicationFactory factory, Guid userId)
    {
        await factory.GetService<IDeviceRepository>().CreateAsync(new Device
        {
            Identifier = DefaultDeviceIdentifier,
            Type = DefaultDeviceType,
            Name = DefaultDeviceName,
            UserId = userId,
        });
    }
}
