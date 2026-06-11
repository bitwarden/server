using System.Net;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.Auth.Models.Request;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Tokens;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace Bit.Api.IntegrationTest.Controllers;

public class EmergencyAccessControllerTest : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private static readonly string _masterKeyWrappedUserKey =
        "2.AOs41Hd8OQiCPXjyJKCiDA==|O6OHgt2U2hJGBSNGnimJmg==|iD33s8B69C8JhYYhSa4V1tArjvLr8eEaGqOV7BRo5Jk=";

    private static readonly string _newMasterPasswordHash = "new_master_password_hash";

    private static readonly KdfRequestModel _defaultKdfRequest =
        new() { KdfType = KdfType.PBKDF2_SHA256, Iterations = 600_000 };

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private readonly IUserRepository _userRepository;
    private readonly IEmergencyAccessRepository _emergencyAccessRepository;
    private readonly IPasswordHasher<User> _passwordHasher;

    private string _grantorEmail = null!;
    private string _granteeEmail = null!;
    private Guid _emergencyAccessId;

    public EmergencyAccessControllerTest(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.SubstituteService<IPushNotificationService>(_ => { });
        _factory.SubstituteService<IStripeSyncService>(_ => { });
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
        _userRepository = _factory.GetService<IUserRepository>();
        _emergencyAccessRepository = _factory.GetService<IEmergencyAccessRepository>();
        _passwordHasher = _factory.GetService<IPasswordHasher<User>>();
    }

    public async Task InitializeAsync()
    {
        // Two distinct registered accounts: the grantee (caller) and the grantor
        // (whose master password is replaced). Only the grantee needs auth tokens.
        var suffix = Guid.NewGuid();
        _grantorEmail = $"emergency-access-grantor-{suffix}@bitwarden.com";
        _granteeEmail = $"emergency-access-grantee-{suffix}@bitwarden.com";
        await _factory.LoginWithNewAccount(_grantorEmail);
        await _factory.LoginWithNewAccount(_granteeEmail);

        // Seed an emergency access in RecoveryApproved/Takeover — the only state
        // pair IsValidRequest accepts for the Password endpoint. Without this the
        // controller would 400 before model-binding errors get a chance to surface.
        var grantor = await _userRepository.GetByEmailAsync(_grantorEmail);
        var grantee = await _userRepository.GetByEmailAsync(_granteeEmail);
        Assert.NotNull(grantor);
        Assert.NotNull(grantee);

        var emergencyAccess = await _emergencyAccessRepository.CreateAsync(new EmergencyAccess
        {
            GrantorId = grantor.Id,
            GranteeId = grantee.Id,
            Type = EmergencyAccessType.Takeover,
            Status = EmergencyAccessStatusType.RecoveryApproved,
            WaitTimeDays = 1,
            KeyEncrypted = _masterKeyWrappedUserKey,
        });
        _emergencyAccessId = emergencyAccess.Id;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    // Builders for the dual-payload auth + unlock blocks used by every V2
    // password test below. Tests vary KDF and salt; everything else is constant.
    private static MasterPasswordAuthenticationDataRequestModel BuildAuthData(KdfRequestModel kdf, string salt) =>
        new() { Kdf = kdf, MasterPasswordAuthenticationHash = _newMasterPasswordHash, Salt = salt };

    private static MasterPasswordUnlockDataRequestModel BuildUnlockData(KdfRequestModel kdf, string salt) =>
        new() { Kdf = kdf, MasterKeyWrappedUserKey = _masterKeyWrappedUserKey, Salt = salt };

    // Builds an (auth, unlock) pair where one side is perturbed so the agreement
    // validator must fire. Used by PostPassword_V2_MismatchedKdfOrSalt_BadRequest.
    //   mismatchKind:  "kdf" | "salt"  — which field disagrees
    //   perturbedSide: "auth" | "unlock" — which half carries the bad value
    private (MasterPasswordAuthenticationDataRequestModel auth, MasterPasswordUnlockDataRequestModel unlock)
        BuildMismatchedAuthAndUnlock(string mismatchKind, string perturbedSide)
    {
        var perturbedKdf = mismatchKind == "kdf"
            ? new KdfRequestModel { KdfType = KdfType.PBKDF2_SHA256, Iterations = 700_000 }
            : _defaultKdfRequest;
        var perturbedSalt = mismatchKind == "salt" ? "different-salt@bitwarden.com" : _grantorEmail;

        return perturbedSide == "auth"
            ? (BuildAuthData(perturbedKdf, perturbedSalt), BuildUnlockData(_defaultKdfRequest, _grantorEmail))
            : (BuildAuthData(_defaultKdfRequest, _grantorEmail), BuildUnlockData(perturbedKdf, perturbedSalt));
    }

    /// <summary>
    /// Verifies the dual-payload emergency-access takeover path accepts grantors
    /// whose stored KDF predates the current minimum. <c>ValidateKdfAndSaltAgreement</c>
    /// on this endpoint must enforce agreement between <c>AuthenticationData</c> and
    /// <c>UnlockData</c>, not range — otherwise a grantee cannot rescue a legacy
    /// account because the new auth hash is derived client-side against the
    /// grantor's existing KDF.
    /// <para>
    /// Scope: end-to-end through the V2 path; also asserts the grantor's KDF is
    /// left untouched (the <c>PrepareUpdateExistingMasterPasswordAsync</c> path
    /// updates the password only), the security stamp rotated, and the takeover
    /// side effects fired (2FA cleared, device verification disabled) so the
    /// grantee can subsequently log in as the grantor.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(KdfType.PBKDF2_SHA256, 100_000, null, null)] // plausible legacy: real pre-600k users
    [InlineData(KdfType.PBKDF2_SHA256, 5_000, null, null)]   // far below minimum: no soft floor either
    [InlineData(KdfType.Argon2id, 2, 14, 4)]                 // barely sub-minimum: 1mb below memory floor
    [InlineData(KdfType.Argon2id, 1, 8, 1)]                  // far below minimum: no Argon2-specific gate
    public async Task PostPassword_V2_LegacyKdfBelowMinimum_Success(
        KdfType kdf, int kdfIterations, int? kdfMemory, int? kdfParallelism)
    {
        // Arrange: downgrade the grantor's KDF to a sub-minimum value to
        // simulate a real legacy-KDF account that predates the current floor.
        // ValidateDataForUser will only accept the request if its KDF matches
        // the grantor's stored KDF, so the request must echo this downgrade.
        // Also prime 2FA so the post-takeover "cleared" assertion is meaningful
        // (test-factory users have no providers configured by default).
        var grantor = await _userRepository.GetByEmailAsync(_grantorEmail);
        Assert.NotNull(grantor);
        grantor.Kdf = kdf;
        grantor.KdfIterations = kdfIterations;
        grantor.KdfMemory = kdfMemory;
        grantor.KdfParallelism = kdfParallelism;
        grantor.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            [TwoFactorProviderType.Email] = new() { Enabled = true },
        });
        await _userRepository.ReplaceAsync(grantor);
        var grantorSecurityStampBefore = grantor.SecurityStamp;
        var grantorTwoFactorProvidersBefore = grantor.TwoFactorProviders;

        await _loginHelper.LoginAsync(_granteeEmail);

        var legacyKdfRequest = new KdfRequestModel
        {
            KdfType = kdf,
            Iterations = kdfIterations,
            Memory = kdfMemory,
            Parallelism = kdfParallelism,
        };

        // Salt must equal the grantor's stored salt (falls back to email when
        // MasterPasswordSalt is null, which is the case for test-factory users).
        var requestModel = new EmergencyAccessPasswordRequestModel
        {
            AuthenticationData = BuildAuthData(legacyKdfRequest, _grantorEmail),
            UnlockData = BuildUnlockData(legacyKdfRequest, _grantorEmail),
        };

        // Act: hit the real endpoint so model binding, validation, auth filter,
        // command dispatch, and repository write all run end-to-end.
        using var message = new HttpRequestMessage(
            HttpMethod.Post, $"/emergency-access/{_emergencyAccessId}/password");
        message.Content = JsonContent.Create(requestModel);
        var response = await _client.SendAsync(message);

        // Surface the error body on failure — a bare EnsureSuccessStatusCode
        // hides the validator message that points at any future range-check regression.
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Assert.Fail($"Expected success but got {response.StatusCode}. Error: {errorContent}");
        }

        // Assert: grantor's new password was persisted (rules out a silent no-op).
        var updatedGrantor = await _userRepository.GetByEmailAsync(_grantorEmail);
        Assert.NotNull(updatedGrantor);
        Assert.Equal(PasswordVerificationResult.Success,
            _passwordHasher.VerifyHashedPassword(
                updatedGrantor, updatedGrantor.MasterPassword!, _newMasterPasswordHash));

        // KDF must be unchanged — PrepareUpdateExistingMasterPasswordAsync changes
        // the password only. A silent bump to current minimum would corrupt the
        // account: the new auth hash was derived client-side against the legacy KDF.
        Assert.Equal(kdf, updatedGrantor.Kdf);
        Assert.Equal(kdfIterations, updatedGrantor.KdfIterations);
        Assert.Equal(kdfMemory, updatedGrantor.KdfMemory);
        Assert.Equal(kdfParallelism, updatedGrantor.KdfParallelism);

        // Key (master-key-wrapped user key) and grantor-takeover side effects
        // applied: security stamp rotated, device verification turned off,
        // 2FA providers cleared (otherwise they'd block the grantee's login).
        Assert.Equal(_masterKeyWrappedUserKey, updatedGrantor.Key);
        Assert.NotEqual(grantorSecurityStampBefore, updatedGrantor.SecurityStamp);
        Assert.False(updatedGrantor.VerifyDevices);
        Assert.NotEqual(grantorTwoFactorProvidersBefore, updatedGrantor.TwoFactorProviders);
        Assert.Empty(updatedGrantor.GetTwoFactorProviders() ?? []);
    }

    /// <summary>
    /// Verifies the boundary validator's agreement checks fire on
    /// <c>POST /emergency-access/{id}/password</c>: a mismatched KDF or salt
    /// between <c>AuthenticationData</c> and <c>UnlockData</c> is rejected with
    /// 400. Complements the legacy-KDF success test by proving the agreement
    /// invariant isn't passively letting everything through.
    /// </summary>
    [Theory]
    [InlineData("kdf", "unlock", "AuthenticationData and UnlockData must have the same KDF configuration.")]
    [InlineData("kdf", "auth", "AuthenticationData and UnlockData must have the same KDF configuration.")]
    [InlineData("salt", "unlock", "Invalid master password salt.")]
    [InlineData("salt", "auth", "Invalid master password salt.")]
    public async Task PostPassword_V2_MismatchedKdfOrSalt_BadRequest(
        string mismatchKind, string perturbedSide, string expectedError)
    {
        await _loginHelper.LoginAsync(_granteeEmail);

        var (auth, unlock) = BuildMismatchedAuthAndUnlock(mismatchKind, perturbedSide);
        var requestModel = new EmergencyAccessPasswordRequestModel
        {
            AuthenticationData = auth,
            UnlockData = unlock,
        };

        using var message = new HttpRequestMessage(
            HttpMethod.Post, $"/emergency-access/{_emergencyAccessId}/password");
        message.Content = JsonContent.Create(requestModel);
        var response = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains(expectedError, content);
    }

    /// <summary>
    /// Verifies the controller's domain precondition fires: a Password call
    /// against an emergency access not in <c>RecoveryApproved</c> state must
    /// be rejected with 400 before the master-password service runs. 
    /// </summary>
    [Theory]
    [InlineData(EmergencyAccessStatusType.Invited)]
    [InlineData(EmergencyAccessStatusType.Accepted)]
    [InlineData(EmergencyAccessStatusType.Confirmed)]
    [InlineData(EmergencyAccessStatusType.RecoveryInitiated)]
    public async Task PostPassword_V2_NotRecoveryApproved_BadRequest(
        EmergencyAccessStatusType invalidStatus)
    {
        // Arrange: mutate the seeded record into a non-RecoveryApproved state.
        // IsValidRequest accepts only RecoveryApproved; everything else throws
        // "Emergency Access not valid." before the V2 handler runs — so a well-
        // formed payload still 400s on the precondition rather than the
        // validator.
        var emergencyAccess = await _emergencyAccessRepository.GetByIdAsync(_emergencyAccessId);
        Assert.NotNull(emergencyAccess);
        emergencyAccess.Status = invalidStatus;
        await _emergencyAccessRepository.ReplaceAsync(emergencyAccess);

        await _loginHelper.LoginAsync(_granteeEmail);

        var requestModel = new EmergencyAccessPasswordRequestModel
        {
            AuthenticationData = BuildAuthData(_defaultKdfRequest, _grantorEmail),
            UnlockData = BuildUnlockData(_defaultKdfRequest, _grantorEmail),
        };

        using var message = new HttpRequestMessage(
            HttpMethod.Post, $"/emergency-access/{_emergencyAccessId}/password");
        message.Content = JsonContent.Create(requestModel);
        var response = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Emergency Access not valid.", content);
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
        var invitedEa = await _emergencyAccessRepository.CreateAsync(new EmergencyAccess
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
        message.Content = JsonContent.Create(new OrganizationUserAcceptRequestModel { Token = expiredToken });
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

    /// <summary>
    /// Verifies the dual-presence validator fires when the request carries
    /// neither the V2 (UnlockData/AuthenticationData) nor the V1 (legacy)
    /// payload — the controller must reject empty bodies with 400 rather than
    /// dispatch to either path.
    /// </summary>
    [Fact]
    public async Task PostPassword_NoPayload_BadRequest()
    {
        await _loginHelper.LoginAsync(_granteeEmail);

        var requestModel = new EmergencyAccessPasswordRequestModel();

        using var message = new HttpRequestMessage(
            HttpMethod.Post, $"/emergency-access/{_emergencyAccessId}/password");
        message.Content = JsonContent.Create(requestModel);
        var response = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains(
            "Must provide either new payloads (UnlockData/AuthenticationData) or legacy payloads",
            content);
    }
}
