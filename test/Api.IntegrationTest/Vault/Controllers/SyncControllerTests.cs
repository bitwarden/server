using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Api.Vault.Models.Response;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Api.IntegrationTest.Vault.Controllers;

public class SyncControllerTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;

    private readonly LoginHelper _loginHelper;

    private readonly IUserRepository _userRepository;
    private string _ownerEmail = null!;

    public SyncControllerTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
        _userRepository = _factory.GetService<IUserRepository>();
    }

    public async ValueTask InitializeAsync()
    {
        _ownerEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(_ownerEmail);
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    // [BitAutoData]
    public async Task Get_HaveNoMasterPassword_UserDecryptionMasterPasswordUnlockIsNull()
    {
        var tempEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(tempEmail);
        await _loginHelper.LoginAsync(tempEmail);

        // Remove user's password.
        var user = await _userRepository.GetByEmailAsync(tempEmail);
        Assert.NotNull(user);
        user.MasterPassword = null;
        await _userRepository.UpsertAsync(user);

        var response = await _client.GetAsync("/sync");
        response.EnsureSuccessStatusCode();

        var syncResponseModel = await response.Content.ReadFromJsonAsync<SyncResponseModel>();

        Assert.NotNull(syncResponseModel);
        Assert.NotNull(syncResponseModel.UserDecryption);
        Assert.Null(syncResponseModel.UserDecryption.MasterPasswordUnlock);
    }

    [Theory]
    [BitAutoData(KdfType.PBKDF2_SHA256, 654_321, null, null)]
    [BitAutoData(KdfType.Argon2id, 11, 128, 5)]
    public async Task Get_HaveMasterPassword_UserDecryptionMasterPasswordUnlockNotNull(
        KdfType kdfType, int kdfIterations, int? kdfMemory, int? kdfParallelism)
    {
        var tempEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(tempEmail);
        await _loginHelper.LoginAsync(tempEmail);

        // Change KDF settings
        var user = await _userRepository.GetByEmailAsync(tempEmail);
        Assert.NotNull(user);
        user.Kdf = kdfType;
        user.KdfIterations = kdfIterations;
        user.KdfMemory = kdfMemory;
        user.KdfParallelism = kdfParallelism;
        await _userRepository.UpsertAsync(user);

        var response = await _client.GetAsync("/sync");
        response.EnsureSuccessStatusCode();

        var syncResponseModel = await response.Content.ReadFromJsonAsync<SyncResponseModel>();

        Assert.NotNull(syncResponseModel);
        Assert.NotNull(syncResponseModel.UserDecryption);
        Assert.NotNull(syncResponseModel.UserDecryption.MasterPasswordUnlock);
        Assert.NotNull(syncResponseModel.UserDecryption.MasterPasswordUnlock.Kdf);
        Assert.Equal(kdfType, syncResponseModel.UserDecryption.MasterPasswordUnlock.Kdf.KdfType);
        Assert.Equal(kdfIterations, syncResponseModel.UserDecryption.MasterPasswordUnlock.Kdf.Iterations);
        Assert.Equal(kdfMemory, syncResponseModel.UserDecryption.MasterPasswordUnlock.Kdf.Memory);
        Assert.Equal(kdfParallelism, syncResponseModel.UserDecryption.MasterPasswordUnlock.Kdf.Parallelism);
        Assert.Equal(user.Key, syncResponseModel.UserDecryption.MasterPasswordUnlock.MasterKeyEncryptedUserKey);
        Assert.Equal(user.Email.ToLower(), syncResponseModel.UserDecryption.MasterPasswordUnlock.Salt);
    }
}
