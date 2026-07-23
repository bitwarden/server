using System.Globalization;
using System.Net;
using System.Text.Json;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Api.Request.AuthRequest;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.Helpers;
using Xunit;

namespace Bit.Api.IntegrationTest.Auth.Controllers;

public class AuthRequestsControllerTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
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

    public AuthRequestsControllerTests(ApiApplicationFactory factory)
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

    private async Task<AuthRequest> SeedAuthRequestAsync(Guid userId, Action<AuthRequest>? customize = null)
    {
        var authRequest = new AuthRequest
        {
            UserId = userId,
            Type = AuthRequestType.AuthenticateAndUnlock,
            RequestDeviceIdentifier = "requesting-device-identifier",
            RequestDeviceType = DeviceType.Android,
            RequestIpAddress = "1.1.1.1",
            AccessCode = "test_access_code",
            PublicKey = "test_public_key",
            CreationDate = DateTime.UtcNow,
        };
        customize?.Invoke(authRequest);
        return await _authRequestRepository.CreateAsync(authRequest);
    }

    private async Task<User> RegisterAnotherUserAsync()
    {
        var email = $"auth-request-controller-test-other-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(email);
        var user = await _userRepository.GetByEmailAsync(email);
        Assert.NotNull(user);
        return user;
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

    private static async Task AssertBadRequestAsync(HttpResponseMessage response, string expectedMessage)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains(expectedMessage, content);
    }

    // AuthRequestResponseModel/PendingAuthRequestResponseModel only expose a parameterized constructor (they're
    // built from an AuthRequest entity), so System.Text.Json can't deserialize directly into them. Parse the raw
    // JSON instead, matching the convention already used by ResourceOwnerPasswordValidatorTests.
    private static async Task<JsonElement> AssertOkAndGetJsonAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement;
    }

    // ----- GET /auth-requests -----

    [Fact]
    public async Task GetAll_ReturnsOnlyCurrentUsersRequests()
    {
        var mine = await SeedAuthRequestAsync(_user.Id);
        var otherUser = await RegisterAnotherUserAsync();
        await SeedAuthRequestAsync(otherUser.Id);

        var response = await _client.GetAsync("/auth-requests");

        var root = await AssertOkAndGetJsonAsync(response);
        var data = AssertHelper.AssertJsonProperty(root, "data", JsonValueKind.Array);
        Assert.Equal(1, data.GetArrayLength());
        Assert.Equal(mine.Id, data[0].GetProperty("id").GetGuid());
    }

    // ----- GET /auth-requests/{id} -----

    [Fact]
    public async Task Get_Success_ReturnsAuthRequest()
    {
        var authRequest = await SeedAuthRequestAsync(_user.Id);

        var response = await _client.GetAsync($"/auth-requests/{authRequest.Id}");

        var root = await AssertOkAndGetJsonAsync(response);
        Assert.Equal(authRequest.Id, root.GetProperty("id").GetGuid());
        Assert.Equal(authRequest.PublicKey, root.GetProperty("publicKey").GetString());
    }

    [Fact]
    public async Task Get_NotFound_WhenRequestDoesNotExist()
    {
        var response = await _client.GetAsync($"/auth-requests/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_NotFound_WhenRequestBelongsToAnotherUser()
    {
        var otherUser = await RegisterAnotherUserAsync();
        var authRequest = await SeedAuthRequestAsync(otherUser.Id);

        var response = await _client.GetAsync($"/auth-requests/{authRequest.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ----- GET /auth-requests/pending -----

    [Fact]
    public async Task GetPending_ReturnsOnlyUnansweredRequests()
    {
        var pending = await SeedAuthRequestAsync(_user.Id, r =>
        {
            r.RequestDeviceIdentifier = "pending-device-identifier";
            r.Approved = null;
            r.ResponseDate = null;
        });
        // Already-answered requests must not show up as pending.
        await SeedAuthRequestAsync(_user.Id, r =>
        {
            r.RequestDeviceIdentifier = "answered-device-identifier";
            r.Approved = true;
            r.ResponseDate = DateTime.UtcNow;
        });

        var response = await _client.GetAsync("/auth-requests/pending");

        var root = await AssertOkAndGetJsonAsync(response);
        var data = AssertHelper.AssertJsonProperty(root, "data", JsonValueKind.Array);
        Assert.Equal(1, data.GetArrayLength());
        Assert.Equal(pending.Id, data[0].GetProperty("id").GetGuid());
    }

    // ----- GET /auth-requests/{id}/response (anonymous) -----

    [Fact]
    public async Task GetResponse_Success_WhenAccessCodeMatches()
    {
        var authRequest = await SeedAuthRequestAsync(_user.Id, r => r.AccessCode = "matching_code");

        var response = await _anonymousClient.GetAsync($"/auth-requests/{authRequest.Id}/response?code=matching_code");

        var root = await AssertOkAndGetJsonAsync(response);
        Assert.Equal(authRequest.Id, root.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task GetResponse_NotFound_WhenAccessCodeDoesNotMatch()
    {
        var authRequest = await SeedAuthRequestAsync(_user.Id, r => r.AccessCode = "matching_code");

        var response = await _anonymousClient.GetAsync($"/auth-requests/{authRequest.Id}/response?code=wrong_code");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetResponse_NotFound_WhenRequestDoesNotExist()
    {
        var response = await _anonymousClient.GetAsync($"/auth-requests/{Guid.NewGuid()}/response?code=whatever");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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

    // ----- PUT /auth-requests/{id} (approve/deny) -----

    [Fact]
    public async Task Put_Approve_Success_SetsKeyAndMasterPasswordHash()
    {
        var authRequest = await SeedAuthRequestAsync(_user.Id, r =>
        {
            r.Approved = null;
            r.ResponseDate = null;
        });

        var model = new AuthRequestUpdateRequestModel
        {
            DeviceIdentifier = IdentityApplicationFactory.DefaultDeviceIdentifier,
            RequestApproved = true,
            Key = "wrapped-user-key",
            MasterPasswordHash = "master-password-hash",
        };

        var response = await _client.PutAsJsonAsync($"/auth-requests/{authRequest.Id}", model);

        var root = await AssertOkAndGetJsonAsync(response);
        Assert.True(root.GetProperty("requestApproved").GetBoolean());
        Assert.Equal("wrapped-user-key", root.GetProperty("key").GetString());

        var stored = await _authRequestRepository.GetByIdAsync(authRequest.Id);
        Assert.NotNull(stored);
        Assert.True(stored.Approved);
        Assert.NotNull(stored.ResponseDate);
        Assert.NotNull(stored.ResponseDeviceId);
        Assert.Equal("wrapped-user-key", stored.Key);
        Assert.Equal("master-password-hash", stored.MasterPasswordHash);
    }

    [Fact]
    public async Task Put_Deny_Success()
    {
        var authRequest = await SeedAuthRequestAsync(_user.Id, r =>
        {
            r.Approved = null;
            r.ResponseDate = null;
        });

        var model = new AuthRequestUpdateRequestModel
        {
            DeviceIdentifier = IdentityApplicationFactory.DefaultDeviceIdentifier,
            RequestApproved = false,
        };

        var response = await _client.PutAsJsonAsync($"/auth-requests/{authRequest.Id}", model);

        var root = await AssertOkAndGetJsonAsync(response);
        Assert.False(root.GetProperty("requestApproved").GetBoolean());

        var stored = await _authRequestRepository.GetByIdAsync(authRequest.Id);
        Assert.NotNull(stored);
        Assert.False(stored.Approved);
    }

    [Fact]
    public async Task Put_BadRequest_WhenRequestAlreadyAnswered()
    {
        var authRequest = await SeedAuthRequestAsync(_user.Id, r =>
        {
            r.Approved = true;
            r.ResponseDate = DateTime.UtcNow;
        });

        var model = new AuthRequestUpdateRequestModel
        {
            DeviceIdentifier = IdentityApplicationFactory.DefaultDeviceIdentifier,
            // Deny (not approve) - an already-answered request is excluded from the "pending" list entirely, so
            // RequestApproved = true would hit ValidateApprovalOfMostRecentAuthRequest's "no longer valid" check
            // before ever reaching the Duplicate check this test targets.
            RequestApproved = false,
        };

        var response = await _client.PutAsJsonAsync($"/auth-requests/{authRequest.Id}", model);

        await AssertBadRequestAsync(response, "An authentication request with the same device already exists.");
    }

    [Fact]
    public async Task Put_NotFound_WhenRequestDoesNotExist()
    {
        var model = new AuthRequestUpdateRequestModel
        {
            DeviceIdentifier = IdentityApplicationFactory.DefaultDeviceIdentifier,
            RequestApproved = false,
        };

        var response = await _client.PutAsJsonAsync($"/auth-requests/{Guid.NewGuid()}", model);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(AuthRequestType.AuthenticateAndUnlock)]
    [InlineData(AuthRequestType.Unlock)]
    [InlineData(AuthRequestType.AdminApproval)]
    public async Task Put_NotFound_WhenRequestBelongsToAnotherUser(AuthRequestType type)
    {
        var otherUser = await RegisterAnotherUserAsync();
        var authRequest = await SeedAuthRequestAsync(otherUser.Id, r =>
        {
            r.Type = type;
            r.Approved = null;
            r.ResponseDate = null;
        });

        var model = new AuthRequestUpdateRequestModel
        {
            DeviceIdentifier = IdentityApplicationFactory.DefaultDeviceIdentifier,
            RequestApproved = false,
        };

        // _client here is in the context of _user, not otherUser, so this should hit the ownership check and return 404.
        var response = await _client.PutAsJsonAsync($"/auth-requests/{authRequest.Id}", model);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var stored = await _authRequestRepository.GetByIdAsync(authRequest.Id);
        Assert.NotNull(stored);
        Assert.Null(stored.Approved);
    }

    [Fact]
    public async Task Put_BadRequest_WhenNotMostRecentRequestForDevice()
    {
        var older = await SeedAuthRequestAsync(_user.Id, r =>
        {
            r.RequestDeviceIdentifier = "shared-device-identifier";
            r.CreationDate = DateTime.UtcNow.AddMinutes(-10);
            r.Approved = null;
            r.ResponseDate = null;
        });
        await SeedAuthRequestAsync(_user.Id, r =>
        {
            r.RequestDeviceIdentifier = "shared-device-identifier";
            r.CreationDate = DateTime.UtcNow.AddMinutes(-1);
            r.Approved = null;
            r.ResponseDate = null;
        });

        var model = new AuthRequestUpdateRequestModel
        {
            DeviceIdentifier = IdentityApplicationFactory.DefaultDeviceIdentifier,
            RequestApproved = true,
        };

        var response = await _client.PutAsJsonAsync($"/auth-requests/{older.Id}", model);

        await AssertBadRequestAsync(response, "This request is no longer valid. Make sure to approve the most recent request.");
    }

    [Fact]
    public async Task Put_BadRequest_WhenApprovingDeviceIsInvalid()
    {
        var authRequest = await SeedAuthRequestAsync(_user.Id, r =>
        {
            r.Approved = null;
            r.ResponseDate = null;
        });

        var model = new AuthRequestUpdateRequestModel
        {
            // Not a device registered to the current user - deny is used so this exercises the device lookup
            // directly rather than going through ValidateApprovalOfMostRecentAuthRequest first.
            DeviceIdentifier = "unregistered-approving-device",
            RequestApproved = false,
        };

        var response = await _client.PutAsJsonAsync($"/auth-requests/{authRequest.Id}", model);

        await AssertBadRequestAsync(response, "Invalid device.");
    }

    [Fact]
    public async Task Put_NotFound_WhenAdminApprovalRequestExpired()
    {
        var authRequest = await SeedAuthRequestAsync(_user.Id, r =>
        {
            r.Type = AuthRequestType.AdminApproval;
            r.CreationDate = DateTime.UtcNow.AddDays(-8); // default AdminRequestExpiration is 7 days
            r.Approved = null;
            r.ResponseDate = null;
        });

        var model = new AuthRequestUpdateRequestModel
        {
            DeviceIdentifier = IdentityApplicationFactory.DefaultDeviceIdentifier,
            RequestApproved = false,
        };

        var response = await _client.PutAsJsonAsync($"/auth-requests/{authRequest.Id}", model);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
