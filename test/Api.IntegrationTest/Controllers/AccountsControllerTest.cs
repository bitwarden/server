#nullable enable
using System.Net;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Api.Models.Response;
using Bit.Core;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Api.IntegrationTest.Controllers;

public class AccountsControllerTest : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private static readonly string _mockEncryptedString =
        "2.AOs41Hd8OQiCPXjyJKCiDA==|O6OHgt2U2hJGBSNGnimJmg==|iD33s8B69C8JhYYhSa4V1tArjvLr8eEaGqOV7BRo5Jk=";

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private readonly IUserRepository _userRepository;
    private string _ownerEmail = null!;
    private string _masterPasswordHash = null!;

    public AccountsControllerTest(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
        _userRepository = _factory.GetService<IUserRepository>();
    }

    public async Task InitializeAsync()
    {
        _ownerEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        _masterPasswordHash = "master_password_hash";
        await _factory.LoginWithNewAccount(_ownerEmail, _masterPasswordHash);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetAccountsProfile_success()
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        using var message = new HttpRequestMessage(HttpMethod.Get, "/accounts/profile");
        var response = await _client.SendAsync(message);

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadFromJsonAsync<ProfileResponseModel>();
        Assert.NotNull(content);
        Assert.Equal(_ownerEmail, content.Email);
        Assert.NotNull(content.Name);
        Assert.True(content.EmailVerified);
        Assert.False(content.Premium);
        Assert.False(content.PremiumFromOrganization);
        Assert.Equal("en-US", content.Culture);
        Assert.NotNull(content.Key);
        Assert.NotNull(content.PrivateKey);
        Assert.NotNull(content.SecurityStamp);
    }

    [Theory]
    [InlineData(KdfType.PBKDF2_SHA256, 600001, null, null)]
    [InlineData(KdfType.Argon2id, 4, 65, 5)]
    public async Task PostKdf_ValidRequest_Success(KdfType kdf, int kdfIterations, int? kdfMemory, int? kdfParallelism)
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        var requestModel = new KdfRequestModel
        {
            MasterPasswordHash = "master_password_hash",
            NewMasterPasswordHash = "new_master_password_hash",
            Key = _mockEncryptedString,
            Kdf = kdf,
            KdfIterations = kdfIterations,
            KdfMemory = kdfMemory,
            KdfParallelism = kdfParallelism
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, "/accounts/kdf");
        message.Content = JsonContent.Create(requestModel);
        var response = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Validate that the user fields were updated correctly
        var user = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(user);
        Assert.Equal(requestModel.Kdf, user.Kdf);
        Assert.Equal(requestModel.KdfIterations, user.KdfIterations);
        Assert.Equal(requestModel.KdfMemory, user.KdfMemory);
        Assert.Equal(requestModel.KdfParallelism, user.KdfParallelism);
        Assert.Equal(requestModel.Key, user.Key);
        Assert.NotNull(user.LastKdfChangeDate);
        Assert.True(user.LastKdfChangeDate > DateTime.UtcNow.AddMinutes(-1));
        Assert.True(user.RevisionDate > DateTime.UtcNow.AddMinutes(-1));
        Assert.True(user.AccountRevisionDate > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task PostKdf_InvalidMasterPasswordHash_BadRequest()
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        var requestModel = new KdfRequestModel
        {
            MasterPasswordHash = "wrong_password_hash",
            NewMasterPasswordHash = _masterPasswordHash,
            Key = _mockEncryptedString,
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = 600_000
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, "/accounts/kdf");
        message.Content = JsonContent.Create(requestModel);
        var response = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Incorrect password", content);
    }

    [Theory]
    [InlineData(KdfType.PBKDF2_SHA256, 1, null, null, "KDF iterations")]
    [InlineData(KdfType.Argon2id, 4, null, 5, "Argon2 memory")]
    [InlineData(KdfType.Argon2id, 4, 65, null, "Argon2 parallelism")]
    public async Task PostKdf_InvalidParameters_BadRequest(KdfType kdf, int kdfIterations, int? kdfMemory,
        int? kdfParallelism, string expectedError)
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        var requestModel = new KdfRequestModel
        {
            MasterPasswordHash = _masterPasswordHash,
            NewMasterPasswordHash = _masterPasswordHash,
            Key = _mockEncryptedString,
            Kdf = kdf,
            KdfIterations = kdfIterations,
            KdfMemory = kdfMemory,
            KdfParallelism = kdfParallelism
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, "/accounts/kdf");
        message.Content = JsonContent.Create(requestModel);
        var response = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains(expectedError, content);
    }

    [Fact]
    public async Task PostKdf_Unauthorized_ReturnsUnauthorized()
    {
        // Don't call LoginAsync to test unauthorized access
        var requestModel = new KdfRequestModel
        {
            MasterPasswordHash = "master_password_hash",
            NewMasterPasswordHash = "new_master_password_hash",
            Key = _mockEncryptedString,
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = AuthConstants.PBKDF2_ITERATIONS.Default
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, "/accounts/kdf");
        // No authorization header
        message.Content = JsonContent.Create(requestModel);
        var response = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
