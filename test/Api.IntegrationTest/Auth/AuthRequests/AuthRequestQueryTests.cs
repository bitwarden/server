using System.Net;
using System.Text.Json;
using Bit.Api.IntegrationTest.Auth.Helpers;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.Auth.Entities;
using Bit.Core.Entities;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Test.Common.Helpers;
using Xunit;
using static Bit.Api.IntegrationTest.Auth.Helpers.AuthRequestAssertions;

namespace Bit.Api.IntegrationTest.Auth.AuthRequests;

/// <summary>
/// Covers reading auth requests: the list, a single request, the pending list, and the anonymous response read.
/// </summary>
public class AuthRequestQueryTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
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

    public AuthRequestQueryTests(ApiApplicationFactory factory)
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

    private Task<AuthRequest> SeedAuthRequestAsync(Guid userId, Action<AuthRequest>? customize = null) =>
        AuthRequestTestHelpers.SeedAuthRequestAsync(_authRequestRepository, userId, customize);

    private Task<User> RegisterAnotherUserAsync() => AuthRequestTestHelpers.RegisterAnotherUserAsync(_factory);

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
}
