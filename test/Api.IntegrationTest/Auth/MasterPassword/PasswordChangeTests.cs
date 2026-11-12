using System.Net;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Core.Models;
using Bit.Core.Models.Data;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Xunit;
using static Bit.Api.IntegrationTest.Auth.Helpers.MasterPasswordPayloads;

namespace Bit.Api.IntegrationTest.Auth.MasterPassword;

public class PasswordChangeTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private readonly IUserRepository _userRepository;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IEventRepository _eventRepository;

    private string _ownerEmail = null!;

    public PasswordChangeTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.SubstituteService<IPushNotificationService>(_ => { });
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
        _userRepository = _factory.GetService<IUserRepository>();
        _pushNotificationService = _factory.GetService<IPushNotificationService>();
        _passwordHasher = _factory.GetService<IPasswordHasher<User>>();
        _eventRepository = _factory.GetService<IEventRepository>();
    }

    public async Task InitializeAsync()
    {
        _ownerEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(_ownerEmail);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies the dual-payload self-service password change path accepts
    /// legacy-KDF users — those whose stored KDF predates the current minimum.
    /// <c>ValidateKdfAndSaltAgreement</c> must check agreement between
    /// <c>AuthenticationData</c> and <c>UnlockData</c>, not range.
    /// <para>
    /// Scope: end-to-end through the V2 path; also asserts the KDF is left
    /// untouched (a silent server-side bump would corrupt the account, since
    /// the new auth hash was derived client-side against the legacy KDF) and
    /// that other-device logout fires.
    /// </para>
    /// <para>
    /// Note: mutating the KDF columns below does not invalidate the seeded
    /// password — <c>user.MasterPassword</c> is Identity's <c>IPasswordHasher</c>
    /// output over the wire-sent hash, not derived from <c>user.Kdf</c>.
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
        // Arrange: downgrade the seeded user's KDF to a sub-minimum value to
        // simulate a real legacy-KDF account that predates the current floor.
        var user = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(user);
        user.Kdf = kdf;
        user.KdfIterations = kdfIterations;
        user.KdfMemory = kdfMemory;
        user.KdfParallelism = kdfParallelism;
        await _userRepository.ReplaceAsync(user);

        await _loginHelper.LoginAsync(_ownerEmail);

        // Both halves of the V2 payload echo the user's legacy KDF — agreement,
        // not range, is what ValidateKdfAndSaltAgreement enforces.
        var legacyKdfRequest = new KdfRequestModel
        {
            KdfType = kdf,
            Iterations = kdfIterations,
            Memory = kdfMemory,
            Parallelism = kdfParallelism,
        };

        // Populating AuthenticationData + UnlockData routes the controller to
        // the new SelfServicePasswordChangeCommand (V2) path; the legacy
        // NewMasterPasswordHash / Key fields are accepted but ignored.
        var requestModel = new PasswordRequestModel
        {
            MasterPasswordHash = MasterPasswordHash,
            NewMasterPasswordHash = NewMasterPasswordHash,
            Key = MasterKeyWrappedUserKey,
            AuthenticationData = BuildAuthData(legacyKdfRequest, _ownerEmail),
            UnlockData = BuildUnlockData(legacyKdfRequest, _ownerEmail),
        };

        // Act: hit the real endpoint so model binding, validation, auth filter,
        // command dispatch, and repository write all run end-to-end.
        using var message = new HttpRequestMessage(HttpMethod.Post, "/accounts/password");
        message.Content = JsonContent.Create(requestModel);
        var response = await _client.SendAsync(message);

        // Surface the error body on failure — a bare EnsureSuccessStatusCode
        // hides the validator message that points at any future range-check regression.
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Assert.Fail($"Expected success but got {response.StatusCode}. Error: {errorContent}");
        }

        // Assert: new password was persisted (rules out a silent no-op success).
        var updatedUser = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(updatedUser);
        Assert.Equal(PasswordVerificationResult.Success,
            _passwordHasher.VerifyHashedPassword(updatedUser, updatedUser.MasterPassword!, NewMasterPasswordHash));

        // KDF must be unchanged — this flow changes the password only. A silent
        // bump to current minimum would corrupt the account: the new auth hash
        // was derived client-side against the legacy KDF.
        Assert.Equal(kdf, updatedUser.Kdf);
        Assert.Equal(kdfIterations, updatedUser.KdfIterations);
        Assert.Equal(kdfMemory, updatedUser.KdfMemory);
        Assert.Equal(kdfParallelism, updatedUser.KdfParallelism);

        // Other devices are logged out, current session is preserved
        // (excludeCurrentContextFromPush: true) — self-service-specific behavior.
        await _pushNotificationService.Received(1).PushAsync(Arg.Is<PushNotification<LogOutPushNotification>>(n => n.Type == PushType.LogOut && n.TargetId == updatedUser.Id && n.ExcludeCurrentContext));

        // User_ChangedPassword event was logged.
        var events = await _eventRepository.GetManyByUserAsync(
            updatedUser.Id, DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(1),
            new PageOptions { PageSize = 100 });
        Assert.Contains(events.Data, e => e.Type == EventType.User_ChangedPassword && e.UserId == updatedUser.Id);
    }

    /// <summary>
    /// Verifies the boundary validator's agreement checks fire on
    /// <c>POST /accounts/password</c>: a mismatched KDF or salt between
    /// <c>AuthenticationData</c> and <c>UnlockData</c> is rejected with 400.
    /// Complements the legacy-KDF success test by proving the agreement
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
        await _loginHelper.LoginAsync(_ownerEmail);

        var (auth, unlock) = BuildMismatchedAuthAndUnlock(mismatchKind, perturbedSide, _ownerEmail);
        var requestModel = new PasswordRequestModel
        {
            MasterPasswordHash = MasterPasswordHash,
            NewMasterPasswordHash = NewMasterPasswordHash,
            Key = MasterKeyWrappedUserKey,
            AuthenticationData = auth,
            UnlockData = unlock,
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, "/accounts/password");
        message.Content = JsonContent.Create(requestModel);
        var response = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains(expectedError, content);
    }

    [Fact]
    public async Task PostPassword_V2_Unauthorized_ReturnsUnauthorized()
    {
        // No LoginAsync — the request is anonymous and must be rejected before
        // it reaches the action body.
        var requestModel = new PasswordRequestModel
        {
            MasterPasswordHash = MasterPasswordHash,
            NewMasterPasswordHash = NewMasterPasswordHash,
            Key = MasterKeyWrappedUserKey,
            AuthenticationData = BuildAuthData(DefaultKdfRequest, _ownerEmail),
            UnlockData = BuildUnlockData(DefaultKdfRequest, _ownerEmail),
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, "/accounts/password");
        message.Content = JsonContent.Create(requestModel);
        var response = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostPassword_V2_InvalidMasterPasswordHash_BadRequest()
    {
        // Self-service change-password verifies the current password before
        // applying the new one. A wrong current hash must be rejected.
        await _loginHelper.LoginAsync(_ownerEmail);

        var requestModel = new PasswordRequestModel
        {
            MasterPasswordHash = "wrong-master-password-hash",
            NewMasterPasswordHash = NewMasterPasswordHash,
            Key = MasterKeyWrappedUserKey,
            AuthenticationData = BuildAuthData(DefaultKdfRequest, _ownerEmail),
            UnlockData = BuildUnlockData(DefaultKdfRequest, _ownerEmail),
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, "/accounts/password");
        message.Content = JsonContent.Create(requestModel);
        var response = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Incorrect password", content);
    }
}
