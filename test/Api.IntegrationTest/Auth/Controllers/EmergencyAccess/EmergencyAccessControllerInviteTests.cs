using System.Net;
using Bit.Api.Auth.Models.Request;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Billing.Services;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Tokens;
using Xunit;

namespace Bit.Api.IntegrationTest.Auth.Controllers.EmergencyAccess;

/// <summary>
/// Covers the grantee accepting an emergency access invitation.
/// </summary>
public class EmergencyAccessControllerInviteTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private readonly IUserRepository _userRepository;
    private readonly IEmergencyAccessRepository _emergencyAccessRepository;

    private string _grantorEmail = null!;
    private string _granteeEmail = null!;

    public EmergencyAccessControllerInviteTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.SubstituteService<IPushNotificationService>(_ => { });
        _factory.SubstituteService<IStripeSyncService>(_ => { });
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
        _userRepository = _factory.GetService<IUserRepository>();
        _emergencyAccessRepository = _factory.GetService<IEmergencyAccessRepository>();
    }

    public async Task InitializeAsync()
    {
        // Two distinct registered accounts: the grantor who sends the invitation and the grantee who accepts it.
        var suffix = Guid.NewGuid();
        _grantorEmail = $"emergency-access-grantor-{suffix}@bitwarden.com";
        _granteeEmail = $"emergency-access-grantee-{suffix}@bitwarden.com";
        await _factory.LoginWithNewAccount(_grantorEmail);
        await _factory.LoginWithNewAccount(_granteeEmail);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies the accept-invite call site composes its token guard correctly
    /// </summary>
    [Fact]
    public async Task PostAccept_ExpiredToken_BadRequest()
    {
        // Seed a separate EA in Invited state addressed to the grantee. The
        // class-level seed targets RecoveryApproved for the password tests; we
        // need a distinct Invited row so the accept-invite handler will reach
        // the token-validation gate.
        var grantor = await _userRepository.GetByEmailAsync(_grantorEmail);
        Assert.NotNull(grantor);
        var invitedEa = await _emergencyAccessRepository.CreateAsync(new Core.Auth.Entities.EmergencyAccess
        {
            GrantorId = grantor.Id,
            Email = _granteeEmail,
            Type = EmergencyAccessType.View,
            Status = EmergencyAccessStatusType.Invited,
            WaitTimeDays = 1,
        });

        // Round-trip through the real DataProtector so TryUnprotect succeeds at
        // the controller — the rejection must come from the .Valid expiration
        // check, not from a parse failure.
        var tokenFactory = _factory.GetService<IDataProtectorTokenFactory<EmergencyAccessInviteTokenable>>();
        var expiredToken = tokenFactory.Protect(new EmergencyAccessInviteTokenable(invitedEa, hoursTillExpiration: -1));

        await _loginHelper.LoginAsync(_granteeEmail);

        using var message = new HttpRequestMessage(HttpMethod.Post, $"/emergency-access/{invitedEa.Id}/accept");
        message.Content = JsonContent.Create(new EmergencyAccessAcceptRequestModel { Token = expiredToken });
        var response = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid token.", content);

        // Row must not have advanced past Invited or picked up a GranteeId.
        var unchanged = await _emergencyAccessRepository.GetByIdAsync(invitedEa.Id);
        Assert.NotNull(unchanged);
        Assert.Equal(EmergencyAccessStatusType.Invited, unchanged.Status);
        Assert.Null(unchanged.GranteeId);
    }
}
