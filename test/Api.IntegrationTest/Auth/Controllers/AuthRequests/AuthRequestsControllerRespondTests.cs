using System.Net;
using Bit.Api.IntegrationTest.Auth.Helpers;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Api.Request.AuthRequest;
using Bit.Core.Entities;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.IntegrationTestCommon.Factories;
using Xunit;
using static Bit.Api.IntegrationTest.Auth.Helpers.AuthRequestAssertions;

namespace Bit.Api.IntegrationTest.Auth.Controllers.AuthRequests;

/// <summary>
/// Covers approving and denying an auth request.
/// </summary>
public class AuthRequestsControllerRespondTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private readonly IUserRepository _userRepository;
    private readonly IAuthRequestRepository _authRequestRepository;

    private string _userEmail = null!;
    private User _user = null!;

    public AuthRequestsControllerRespondTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        // AuthRequestService pushes real notifications on create/approve/deny; the default push service isn't
        // no-op'd by the base factory, so it must be substituted.
        _factory.SubstituteService<IPushNotificationService>(_ => { });
        _client = factory.CreateClient();
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
        return Task.CompletedTask;
    }

    private Task<AuthRequest> SeedAuthRequestAsync(Guid userId, Action<AuthRequest>? customize = null) =>
        AuthRequestTestHelpers.SeedAuthRequestAsync(_authRequestRepository, userId, customize);

    private Task<User> RegisterAnotherUserAsync() => AuthRequestTestHelpers.RegisterAnotherUserAsync(_factory);

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
