using System.Net;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core;
using Bit.Core.Repositories;
using NSubstitute;
using Xunit;
using static Bit.Api.IntegrationTest.Auth.Helpers.MasterPasswordPayloads;

namespace Bit.Api.IntegrationTest.Auth.Controllers.Accounts;

public class AccountsControllerApiKeyTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private readonly IUserRepository _userRepository;
    private readonly Bitwarden.Server.Sdk.Features.IFeatureService _featureService;

    private string _ownerEmail = null!;

    public AccountsControllerApiKeyTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.SubstituteService<Bitwarden.Server.Sdk.Features.IFeatureService>(_ => { });
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
        _userRepository = _factory.GetService<IUserRepository>();
        _featureService = _factory.GetService<Bitwarden.Server.Sdk.Features.IFeatureService>();
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

    // TODO: Delete this test when the PM37165_RotateUserApiKeyCommand flag is cleaned up — the legacy path
    // it covers will be removed along with UserService.RotateApiKeyAsync.
    [Fact]
    public async Task PostRotateApiKey_FlagOff_RotatesApiKey_AndLeavesLastApiKeyRotationDateNull()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM37165_RotateUserApiKeyCommand).Returns(false);
        await _loginHelper.LoginAsync(_ownerEmail);

        var before = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(before);
        var originalApiKey = before.ApiKey;

        var response = await PostRotateApiKeyAsync(MasterPasswordHash);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var after = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(after);
        Assert.NotEqual(originalApiKey, after.ApiKey);
        Assert.Equal(30, after.ApiKey.Length);
        Assert.True(after.RevisionDate > before.RevisionDate);
        Assert.Null(after.LastApiKeyRotationDate);
    }

    // TODO: When the PM37165_RotateUserApiKeyCommand flag is cleaned up, rename this to
    // PostRotateApiKey_RotatesApiKey_AndSetsLastApiKeyRotationDate (and drop the flag setup) — it becomes
    // the canonical happy-path integration test.
    [Fact]
    public async Task PostRotateApiKey_FlagOn_RotatesApiKey_AndSetsLastApiKeyRotationDate()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM37165_RotateUserApiKeyCommand).Returns(true);
        await _loginHelper.LoginAsync(_ownerEmail);

        var before = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(before);
        var originalApiKey = before.ApiKey;

        var response = await PostRotateApiKeyAsync(MasterPasswordHash);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var after = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(after);
        Assert.NotEqual(originalApiKey, after.ApiKey);
        Assert.Equal(30, after.ApiKey.Length);
        Assert.True(after.RevisionDate > before.RevisionDate);
        Assert.NotNull(after.LastApiKeyRotationDate);
        Assert.True(after.LastApiKeyRotationDate > DateTime.UtcNow.AddMinutes(-1));
    }

    // Bad-secret guard runs before the flag branch, but cover both flag states for parity. When the
    // PM37165_RotateUserApiKeyCommand flag is cleaned up, drop the [InlineData] rows and convert back to [Fact].
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PostRotateApiKey_InvalidMasterPasswordHash_BadRequest_AndDoesNotRotate(bool flagOn)
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM37165_RotateUserApiKeyCommand).Returns(flagOn);
        await _loginHelper.LoginAsync(_ownerEmail);

        var before = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(before);

        var response = await PostRotateApiKeyAsync("wrong-master-password-hash");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var after = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(after);
        Assert.Equal(before.ApiKey, after.ApiKey);
        Assert.Null(after.LastApiKeyRotationDate);
    }

    private async Task<HttpResponseMessage> PostRotateApiKeyAsync(string masterPasswordHash)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/accounts/rotate-api-key");
        message.Content = JsonContent.Create(new SecretVerificationRequestModel
        {
            MasterPasswordHash = masterPasswordHash
        });
        return await _client.SendAsync(message);
    }
}
