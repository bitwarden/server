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

namespace Bit.Api.IntegrationTest.Auth.Controllers.Accounts;

public class AccountsControllerTempPasswordTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private readonly IUserRepository _userRepository;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IEventRepository _eventRepository;

    private string _ownerEmail = null!;

    public AccountsControllerTempPasswordTests(ApiApplicationFactory factory)
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
    /// Verifies the dual-payload admin-set-temporary-password replacement path
    /// accepts legacy-KDF users — those whose stored KDF predates the current
    /// minimum. <c>ValidateKdfAndSaltAgreement</c> must check agreement between
    /// <c>AuthenticationData</c> and <c>UnlockData</c>, not range.
    /// <para>
    /// Scope: end-to-end through the V2 path; also asserts the KDF is left
    /// untouched, <c>ForcePasswordReset</c> is cleared, the event is logged,
    /// and logout fires.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(KdfType.PBKDF2_SHA256, 100_000, null, null)] // plausible legacy: real pre-600k users
    [InlineData(KdfType.PBKDF2_SHA256, 5_000, null, null)]   // far below minimum: no soft floor either
    [InlineData(KdfType.Argon2id, 2, 14, 4)]                 // barely sub-minimum: 1mb below memory floor
    [InlineData(KdfType.Argon2id, 1, 8, 1)]                  // far below minimum: no Argon2-specific gate
    public async Task PutUpdateTempPassword_V2_LegacyKdfBelowMinimum_Success(
        KdfType kdf, int kdfIterations, int? kdfMemory, int? kdfParallelism)
    {
        // Arrange: downgrade KDF and set ForcePasswordReset so the command's
        // precondition (admin-set temp password is pending) is satisfied.
        var user = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(user);
        user.Kdf = kdf;
        user.KdfIterations = kdfIterations;
        user.KdfMemory = kdfMemory;
        user.KdfParallelism = kdfParallelism;
        user.ForcePasswordReset = true;
        await _userRepository.ReplaceAsync(user);

        await _loginHelper.LoginAsync(_ownerEmail);

        var legacyKdfRequest = new KdfRequestModel
        {
            KdfType = kdf,
            Iterations = kdfIterations,
            Memory = kdfMemory,
            Parallelism = kdfParallelism,
        };

        var requestModel = new UpdateTempPasswordRequestModel
        {
            NewMasterPasswordHash = NewMasterPasswordHash,
            Key = MasterKeyWrappedUserKey,
            AuthenticationData = BuildAuthData(legacyKdfRequest, _ownerEmail),
            UnlockData = BuildUnlockData(legacyKdfRequest, _ownerEmail),
        };

        using var message = new HttpRequestMessage(HttpMethod.Put, "/accounts/update-temp-password");
        message.Content = JsonContent.Create(requestModel);
        var response = await _client.SendAsync(message);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Assert.Fail($"Expected success but got {response.StatusCode}. Error: {errorContent}");
        }

        var updatedUser = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(updatedUser);
        Assert.Equal(PasswordVerificationResult.Success,
            _passwordHasher.VerifyHashedPassword(updatedUser, updatedUser.MasterPassword!, NewMasterPasswordHash));

        // KDF unchanged — this flow swaps the password only.
        Assert.Equal(kdf, updatedUser.Kdf);
        Assert.Equal(kdfIterations, updatedUser.KdfIterations);
        Assert.Equal(kdfMemory, updatedUser.KdfMemory);
        Assert.Equal(kdfParallelism, updatedUser.KdfParallelism);

        // The "temp password pending" flag is cleared once the user replaces it.
        Assert.False(updatedUser.ForcePasswordReset);

        // All sessions (including the current one) are logged out
        // (excludeCurrentContextFromPush: false, the default) — admin-set temp
        // password flow.
        await _pushNotificationService.Received(1).PushAsync(Arg.Is<PushNotification<LogOutPushNotification>>(n => n.Type == PushType.LogOut && n.TargetId == updatedUser.Id));

        var events = await _eventRepository.GetManyByUserAsync(
            updatedUser.Id, DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(1),
            new PageOptions { PageSize = 100 });
        Assert.Contains(events.Data, e => e.Type == EventType.User_UpdatedTempPassword && e.UserId == updatedUser.Id);
    }

    /// <summary>
    /// Verifies the boundary validator's agreement checks fire on
    /// <c>PUT /accounts/update-temp-password</c>: a mismatched KDF or salt
    /// between <c>AuthenticationData</c> and <c>UnlockData</c> is rejected
    /// with 400. ForcePasswordReset state is irrelevant — model validation
    /// runs before the command's preconditions.
    /// </summary>
    [Theory]
    [InlineData("kdf", "unlock", "AuthenticationData and UnlockData must have the same KDF configuration.")]
    [InlineData("kdf", "auth", "AuthenticationData and UnlockData must have the same KDF configuration.")]
    [InlineData("salt", "unlock", "Invalid master password salt.")]
    [InlineData("salt", "auth", "Invalid master password salt.")]
    public async Task PutUpdateTempPassword_V2_MismatchedKdfOrSalt_BadRequest(
        string mismatchKind, string perturbedSide, string expectedError)
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        var (auth, unlock) = BuildMismatchedAuthAndUnlock(mismatchKind, perturbedSide, _ownerEmail);
        var requestModel = new UpdateTempPasswordRequestModel
        {
            NewMasterPasswordHash = NewMasterPasswordHash,
            Key = MasterKeyWrappedUserKey,
            AuthenticationData = auth,
            UnlockData = unlock,
        };

        using var message = new HttpRequestMessage(HttpMethod.Put, "/accounts/update-temp-password");
        message.Content = JsonContent.Create(requestModel);
        var response = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains(expectedError, content);
    }

    [Fact]
    public async Task PutUpdateTempPassword_V2_Unauthorized_ReturnsUnauthorized()
    {
        // No LoginAsync — anonymous requests must be rejected.
        var requestModel = new UpdateTempPasswordRequestModel
        {
            NewMasterPasswordHash = NewMasterPasswordHash,
            Key = MasterKeyWrappedUserKey,
            AuthenticationData = BuildAuthData(DefaultKdfRequest, _ownerEmail),
            UnlockData = BuildUnlockData(DefaultKdfRequest, _ownerEmail),
        };

        using var message = new HttpRequestMessage(HttpMethod.Put, "/accounts/update-temp-password");
        message.Content = JsonContent.Create(requestModel);
        var response = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PutUpdateTempPassword_V2_NoForcePasswordReset_BadRequest()
    {
        // Seeded user has ForcePasswordReset = false (the default) — no
        // admin-set temp password is pending, so the command must reject.
        await _loginHelper.LoginAsync(_ownerEmail);

        var requestModel = new UpdateTempPasswordRequestModel
        {
            NewMasterPasswordHash = NewMasterPasswordHash,
            Key = MasterKeyWrappedUserKey,
            AuthenticationData = BuildAuthData(DefaultKdfRequest, _ownerEmail),
            UnlockData = BuildUnlockData(DefaultKdfRequest, _ownerEmail),
        };

        using var message = new HttpRequestMessage(HttpMethod.Put, "/accounts/update-temp-password");
        message.Content = JsonContent.Create(requestModel);
        var response = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("User does not have a temporary password to update.", content);
    }
}
