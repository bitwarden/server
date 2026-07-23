using System.Net;
using Bit.Api.Auth.Models.Request;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Identity.TokenProviders;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Services;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth;
using Bit.Core.Billing.Enums;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Tokens;
using NSubstitute;
using Xunit;
using static Bit.Api.IntegrationTest.Controllers.TwoFactor.TwoFactorIntegrationTestHelpers;

namespace Bit.Api.IntegrationTest.Controllers.TwoFactor;

public class TwoFactorControllerOrganizationDuoTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private readonly IUserRepository _userRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IDataProtectorTokenFactory<TwoFactorUserVerificationTokenable> _userVerificationTokenFactory;

    private string _userEmail = null!;

    public TwoFactorControllerOrganizationDuoTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.SubstituteService<IPushNotificationService>(_ => { });
        _factory.SubstituteService<IDuoUniversalTokenService>(svc =>
            svc.ValidateDuoConfiguration(default, default, default).ReturnsForAnyArgs(true));
        _factory.SubstituteService<ICompleteTwoFactorWebAuthnRegistrationCommand>(svc =>
            svc.CompleteTwoFactorWebAuthnRegistrationAsync(default!, default, default!, default!).ReturnsForAnyArgs(true));
        _factory.SubstituteService<IDeleteTwoFactorWebAuthnCredentialCommand>(svc =>
            svc.DeleteTwoFactorWebAuthnCredentialAsync(default!, default).ReturnsForAnyArgs(true));
        _factory.SubstituteService<ITwoFactorEmailService>(_ => { });
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
        _userRepository = _factory.GetService<IUserRepository>();
        _organizationRepository = _factory.GetService<IOrganizationRepository>();
        _userVerificationTokenFactory = _factory.GetService<IDataProtectorTokenFactory<TwoFactorUserVerificationTokenable>>();
    }

    public async Task InitializeAsync()
    {
        _userEmail = $"two-factor-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(_userEmail);
        await _loginHelper.LoginAsync(_userEmail);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetOrganizationDuo_ValidSecret_ReturnsTokenUsableForDelete()
    {
        var (org, _) = await OrganizationTestHelpers.SignUpAsync(_factory,
            plan: PlanType.EnterpriseAnnually, ownerEmail: _userEmail, passwordManagerSeats: 1);
        // Refresh the user's JWT so org-membership claims are present for ManagePolicies.
        await _loginHelper.LoginAsync(_userEmail);
        await EnrollOrganizationInDuo(org.Id);

        var getResponse = await _client.PostAsJsonAsync(
            $"/organizations/{org.Id}/two-factor/get-duo",
            new { MasterPasswordHash = MasterPasswordHash });
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var (enabled, uvToken) = await ReadEnabledAndUserVerificationTokenAsync(getResponse, "duo");
        Assert.True(enabled);
        Assert.False(string.IsNullOrEmpty(uvToken));

        var disableResponse = await SendJsonAsync(_client, HttpMethod.Delete,
            $"/organizations/{org.Id}/two-factor/duo",
            new TwoFactorOrganizationDuoDeleteRequestModel { UserVerificationToken = uvToken });
        Assert.Equal(HttpStatusCode.NoContent, disableResponse.StatusCode);

        var refreshedOrg = await _organizationRepository.GetByIdAsync(org.Id);
        Assert.Null(refreshedOrg!.GetTwoFactorProvider(TwoFactorProviderType.OrganizationDuo));
    }

    [Fact]
    public async Task PutOrganizationDuo_ValidToken_UpdatesProvider()
    {
        var (org, _) = await OrganizationTestHelpers.SignUpAsync(_factory,
            plan: PlanType.EnterpriseAnnually, ownerEmail: _userEmail, passwordManagerSeats: 1);
        await _loginHelper.LoginAsync(_userEmail);
        var getResponse = await _client.PostAsJsonAsync(
            $"/organizations/{org.Id}/two-factor/get-duo",
            new { MasterPasswordHash = MasterPasswordHash });
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var (_, uvToken) = await ReadEnabledAndUserVerificationTokenAsync(getResponse, "duo");

        var response = await _client.PutAsJsonAsync(
            $"/organizations/{org.Id}/two-factor/duo",
            new TwoFactorDuoUpdateRequestModel
            {
                ClientId = new string('a', 20),
                ClientSecret = new string('b', 40),
                Host = "api-test.duosecurity.com",
                UserVerificationToken = uvToken,
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var refreshedOrg = await _organizationRepository.GetByIdAsync(org.Id);
        Assert.NotNull(refreshedOrg!.GetTwoFactorProvider(TwoFactorProviderType.OrganizationDuo));
    }

    [Fact]
    public async Task DeleteOrganizationDuo_CrossProviderToken_BadRequest()
    {
        // Token-binding check runs before the ManagePolicies / org-membership check, so an
        // arbitrary org id is fine — the BadRequest fires first.
        var user = (await _userRepository.GetByEmailAsync(_userEmail))!;
        var duoToken = ProtectUserVerificationToken(_userVerificationTokenFactory, user, TwoFactorProviderType.Duo);

        var response = await SendJsonAsync(_client, HttpMethod.Delete,
            $"/organizations/{Guid.NewGuid()}/two-factor/duo",
            new TwoFactorOrganizationDuoDeleteRequestModel { UserVerificationToken = duoToken });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("User verification failed.", await response.Content.ReadAsStringAsync());
    }

    private Task EnrollOrganizationInDuo(Guid organizationId) =>
        SetOrganizationTwoFactorProvidersJsonAsync(
            _organizationRepository, organizationId, BuildOrganizationDuoProvidersJson());
}
