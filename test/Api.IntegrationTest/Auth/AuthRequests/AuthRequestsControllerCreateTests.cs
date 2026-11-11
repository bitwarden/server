using System.Globalization;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Api.Request.AuthRequest;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.IntegrationTestCommon.Factories;
using Xunit;
using static Bit.Api.IntegrationTest.Auth.Helpers.AuthRequestAssertions;

namespace Bit.Api.IntegrationTest.Auth.AuthRequests;

/// <summary>
/// Covers creating auth requests: the anonymous login-with-device request and the authenticated admin request.
/// </summary>
public class AuthRequestsControllerCreateTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    // A second, never-authenticated client used for the two AllowAnonymous endpoints (Post, GetResponse) so
    // CurrentContext.UserId is never populated from a bearer token during those calls.
    private readonly HttpClient _anonymousClient;
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private readonly IUserRepository _userRepository;
    private readonly IAuthRequestRepository _authRequestRepository;

    private string _userEmail = null!;
    private User _user = null!;

    public AuthRequestsControllerCreateTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        // AuthRequestService pushes real notifications on create/approve/deny; the default push service isn't
        // no-op'd by the base factory, so it must be substituted.
        _factory.SubstituteService<IPushNotificationService>(_ => { });
        _client = factory.CreateClient();
        _anonymousClient = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
        _userRepository = _factory.GetService<IUserRepository>();
        _authRequestRepository = _factory.GetService<IAuthRequestRepository>();
    }

    public async Task InitializeAsync()
    {
        _userEmail = $"auth-request-controller-test-{Guid.NewGuid()}@bitwarden.com";

        // Registering + logging in with the default device identifier persists a real Device row.
        // AuthRequests can only come from "Known devices" so we must use the same device to create the auth request.
        await _factory.LoginWithNewAccount(_userEmail);
        await _loginHelper.LoginAsync(_userEmail);

        _user = await _userRepository.GetByEmailAsync(_userEmail);
        Assert.NotNull(_user);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _anonymousClient.Dispose();
        return Task.CompletedTask;
    }

    private static HttpRequestMessage BuildAnonymousPostMessage(
        AuthRequestCreateRequestModel model, DeviceType? deviceTypeHeader = DeviceType.FirefoxBrowser)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/auth-requests")
        {
            Content = JsonContent.Create(model),
        };
        if (deviceTypeHeader.HasValue)
        {
            message.Headers.Add("Device-Type", ((int)deviceTypeHeader.Value).ToString(CultureInfo.InvariantCulture));
        }
        return message;
    }

    // ----- POST /auth-requests (anonymous create) -----

    [Fact]
    public async Task Post_Success_CreatesAuthRequest_ForKnownDevice()
    {
        var model = new AuthRequestCreateRequestModel
        {
            Email = _userEmail,
            PublicKey = "new_public_key",
            DeviceIdentifier = IdentityApplicationFactory.DefaultDeviceIdentifier,
            AccessCode = "new_access_code",
            Type = AuthRequestType.AuthenticateAndUnlock,
        };

        using var message = BuildAnonymousPostMessage(model);
        var response = await _anonymousClient.SendAsync(message);

        var root = await AssertOkAndGetJsonAsync(response);
        Assert.Equal("new_public_key", root.GetProperty("publicKey").GetString());
        Assert.False(root.GetProperty("requestApproved").GetBoolean());

        var id = root.GetProperty("id").GetGuid();
        var stored = await _authRequestRepository.GetManyByUserIdAsync(_user.Id);
        Assert.Contains(stored, r => r.Id == id && r.AccessCode == "new_access_code");
    }

    [Fact]
    public async Task Post_BadRequest_WhenDeviceUnknown()
    {
        var model = new AuthRequestCreateRequestModel
        {
            Email = _userEmail,
            PublicKey = "new_public_key",
            DeviceIdentifier = "some-unregistered-device",
            AccessCode = "new_access_code",
            Type = AuthRequestType.AuthenticateAndUnlock,
        };

        using var message = BuildAnonymousPostMessage(model);
        var response = await _anonymousClient.SendAsync(message);

        await AssertBadRequestAsync(response, "User or known device not found.");
    }

    [Fact]
    public async Task Post_BadRequest_WhenUserNotFound()
    {
        var model = new AuthRequestCreateRequestModel
        {
            Email = $"does-not-exist-{Guid.NewGuid()}@bitwarden.com",
            PublicKey = "new_public_key",
            DeviceIdentifier = IdentityApplicationFactory.DefaultDeviceIdentifier,
            AccessCode = "new_access_code",
            Type = AuthRequestType.AuthenticateAndUnlock,
        };

        using var message = BuildAnonymousPostMessage(model);
        var response = await _anonymousClient.SendAsync(message);

        // Same message as an unknown device - anonymous callers must not be able to distinguish the two.
        await AssertBadRequestAsync(response, "User or known device not found.");
    }

    [Fact]
    public async Task Post_BadRequest_WhenTypeIsAdminApproval()
    {
        var model = new AuthRequestCreateRequestModel
        {
            Email = _userEmail,
            PublicKey = "new_public_key",
            DeviceIdentifier = IdentityApplicationFactory.DefaultDeviceIdentifier,
            AccessCode = "new_access_code",
            Type = AuthRequestType.AdminApproval,
        };

        using var message = BuildAnonymousPostMessage(model);
        var response = await _anonymousClient.SendAsync(message);

        await AssertBadRequestAsync(response, "You must be authenticated to create a request of that type.");
    }

    [Fact]
    public async Task Post_BadRequest_WhenDeviceTypeHeaderMissing()
    {
        var model = new AuthRequestCreateRequestModel
        {
            Email = _userEmail,
            PublicKey = "new_public_key",
            DeviceIdentifier = IdentityApplicationFactory.DefaultDeviceIdentifier,
            AccessCode = "new_access_code",
            Type = AuthRequestType.AuthenticateAndUnlock,
        };

        using var message = BuildAnonymousPostMessage(model, deviceTypeHeader: null);
        var response = await _anonymousClient.SendAsync(message);

        await AssertBadRequestAsync(response, "Device type not provided.");
    }

    // ----- POST /auth-requests/admin-request (authenticated create) -----

    [Fact]
    public async Task PostAdminRequest_Success_WhenUserBelongsToOrganization()
    {
        var (organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory, ownerEmail: _userEmail);

        var model = new AuthRequestCreateRequestModel
        {
            Email = _userEmail,
            PublicKey = "admin_public_key",
            DeviceIdentifier = IdentityApplicationFactory.DefaultDeviceIdentifier,
            AccessCode = "admin_access_code",
            Type = AuthRequestType.AdminApproval,
        };

        var response = await _client.PostAsJsonAsync("/auth-requests/admin-request", model);

        var root = await AssertOkAndGetJsonAsync(response);
        Assert.Equal("admin_public_key", root.GetProperty("publicKey").GetString());

        var stored = await _authRequestRepository.GetByIdAsync(root.GetProperty("id").GetGuid());
        Assert.NotNull(stored);
        Assert.Equal(organization.Id, stored.OrganizationId);
    }

    [Fact]
    public async Task PostAdminRequest_BadRequest_WhenUserBelongsToNoOrganizations()
    {
        var model = new AuthRequestCreateRequestModel
        {
            Email = _userEmail,
            PublicKey = "admin_public_key",
            DeviceIdentifier = IdentityApplicationFactory.DefaultDeviceIdentifier,
            AccessCode = "admin_access_code",
            Type = AuthRequestType.AdminApproval,
        };

        var response = await _client.PostAsJsonAsync("/auth-requests/admin-request", model);

        await AssertBadRequestAsync(response, "User does not belong to any organizations.");
    }

    [Fact]
    public async Task PostAdminRequest_BadRequest_WhenTypeIsNotAdminApproval()
    {
        var model = new AuthRequestCreateRequestModel
        {
            Email = _userEmail,
            PublicKey = "admin_public_key",
            DeviceIdentifier = IdentityApplicationFactory.DefaultDeviceIdentifier,
            AccessCode = "admin_access_code",
            Type = AuthRequestType.AuthenticateAndUnlock,
        };

        var response = await _client.PostAsJsonAsync("/auth-requests/admin-request", model);

        await AssertBadRequestAsync(response, "Invalid AuthRequestType. Expected AdminApproval.");
    }
}
