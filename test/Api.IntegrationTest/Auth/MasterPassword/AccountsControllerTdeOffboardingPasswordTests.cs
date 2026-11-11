using System.Net;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Api.Request;
using Xunit;
using static Bit.Api.IntegrationTest.Auth.Helpers.MasterPasswordPayloads;

namespace Bit.Api.IntegrationTest.Auth.MasterPassword;

public class AccountsControllerTdeOffboardingPasswordTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    private string _ownerEmail = null!;

    public AccountsControllerTdeOffboardingPasswordTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
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
    /// Verifies <c>PUT /accounts/update-tde-offboarding-password</c> rejects
    /// sub-minimum KDF values. Unlike the password-change / temp-password
    /// flows, TDE offboarding sets a brand-new master password and runs the
    /// full <c>ValidateAuthenticationAndUnlockData</c> validator — range
    /// enforcement is required by design. Model validation fires before the
    /// SSO preconditions, so no org setup is needed.
    /// </summary>
    [Theory]
    [InlineData(KdfType.PBKDF2_SHA256, 100_000, null, null, "KDF iterations must be between")]
    [InlineData(KdfType.Argon2id, 1, 64, 4, "Argon2 iterations must be between")]
    [InlineData(KdfType.Argon2id, 3, 14, 4, "Argon2 memory must be between")]
    public async Task PutUpdateTdeOffboardingPassword_V2_BelowMinimumKdf_BadRequest(
        KdfType kdf, int kdfIterations, int? kdfMemory, int? kdfParallelism, string expectedError)
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        var subMinKdfRequest = new KdfRequestModel
        {
            KdfType = kdf,
            Iterations = kdfIterations,
            Memory = kdfMemory,
            Parallelism = kdfParallelism,
        };

        // Auth and unlock fully agree on salt and KDF — the only validator
        // error is the range check firing on the sub-minimum KDF.
        var requestModel = new UpdateTdeOffboardingPasswordRequestModel
        {
            NewMasterPasswordHash = NewMasterPasswordHash,
            Key = MasterKeyWrappedUserKey,
            AuthenticationData = BuildAuthData(subMinKdfRequest, _ownerEmail),
            UnlockData = BuildUnlockData(subMinKdfRequest, _ownerEmail),
        };

        using var message = new HttpRequestMessage(HttpMethod.Put, "/accounts/update-tde-offboarding-password");
        message.Content = JsonContent.Create(requestModel);
        var response = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains(expectedError, content);
    }

    /// <summary>
    /// Verifies the boundary validator's agreement checks fire on
    /// <c>PUT /accounts/update-tde-offboarding-password</c>: a mismatched KDF
    /// or salt between <c>AuthenticationData</c> and <c>UnlockData</c> is
    /// rejected with 400 before any SSO precondition is evaluated.
    /// </summary>
    [Theory]
    [InlineData("kdf", "unlock", "AuthenticationData and UnlockData must have the same KDF configuration.")]
    [InlineData("kdf", "auth", "AuthenticationData and UnlockData must have the same KDF configuration.")]
    [InlineData("salt", "unlock", "Invalid master password salt.")]
    [InlineData("salt", "auth", "Invalid master password salt.")]
    public async Task PutUpdateTdeOffboardingPassword_V2_MismatchedKdfOrSalt_BadRequest(
        string mismatchKind, string perturbedSide, string expectedError)
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        var (auth, unlock) = BuildMismatchedAuthAndUnlock(mismatchKind, perturbedSide, _ownerEmail);
        var requestModel = new UpdateTdeOffboardingPasswordRequestModel
        {
            NewMasterPasswordHash = NewMasterPasswordHash,
            Key = MasterKeyWrappedUserKey,
            AuthenticationData = auth,
            UnlockData = unlock,
        };

        using var message = new HttpRequestMessage(HttpMethod.Put, "/accounts/update-tde-offboarding-password");
        message.Content = JsonContent.Create(requestModel);
        var response = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains(expectedError, content);
    }

    [Fact]
    public async Task PutUpdateTdeOffboardingPassword_V2_Unauthorized_ReturnsUnauthorized()
    {
        // No LoginAsync — anonymous requests must be rejected.
        var requestModel = new UpdateTdeOffboardingPasswordRequestModel
        {
            NewMasterPasswordHash = NewMasterPasswordHash,
            Key = MasterKeyWrappedUserKey,
            AuthenticationData = BuildAuthData(DefaultKdfRequest, _ownerEmail),
            UnlockData = BuildUnlockData(DefaultKdfRequest, _ownerEmail),
        };

        using var message = new HttpRequestMessage(HttpMethod.Put, "/accounts/update-tde-offboarding-password");
        message.Content = JsonContent.Create(requestModel);
        var response = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
