using System.Net;
using System.Text.Json.Nodes;
using Bit.Api.Auth.Models.Request;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Identity.TokenProviders;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Services;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Tokens;
using NSubstitute;
using OtpNet;
using Xunit;
using static Bit.Api.IntegrationTest.Controllers.TwoFactor.TwoFactorIntegrationTestHelpers;

namespace Bit.Api.IntegrationTest.Controllers.TwoFactor;

public class TwoFactorControllerAuthenticatorTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private readonly IUserRepository _userRepository;
    private readonly IDataProtectorTokenFactory<TwoFactorAuthenticatorUserVerificationTokenable> _authenticatorTokenFactory;

    private string _userEmail = null!;

    public TwoFactorControllerAuthenticatorTests(ApiApplicationFactory factory)
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
        _authenticatorTokenFactory = _factory.GetService<IDataProtectorTokenFactory<TwoFactorAuthenticatorUserVerificationTokenable>>();
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
    public async Task PutAuthenticator_ExpiredToken_BadRequest()
    {
        var user = await _userRepository.GetByEmailAsync(_userEmail);
        var expiredToken = _authenticatorTokenFactory.Protect(
            new TwoFactorAuthenticatorUserVerificationTokenable(user!, AuthenticatorKey)
            {
                ExpirationDate = DateTime.UtcNow.AddMinutes(-1),
            });

        var response = await _client.PutAsJsonAsync("/two-factor/authenticator",
            new TwoFactorAuthenticatorUpdateRequestModel
            {
                Token = "123456",
                Key = AuthenticatorKey,
                UserVerificationToken = expiredToken,
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("User verification failed.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DeleteAuthenticator_ExpiredToken_BadRequest()
    {
        var user = await _userRepository.GetByEmailAsync(_userEmail);
        var expiredToken = _authenticatorTokenFactory.Protect(
            new TwoFactorAuthenticatorUserVerificationTokenable(user!, AuthenticatorKey)
            {
                ExpirationDate = DateTime.UtcNow.AddMinutes(-1),
            });

        var response = await SendJsonAsync(_client, HttpMethod.Delete, "/two-factor/authenticator",
            new TwoFactorAuthenticatorDeleteRequestModel
            {
                Key = AuthenticatorKey,
                UserVerificationToken = expiredToken,
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("User verification failed.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetAuthenticator_ValidSecret_ReturnsTokenUsableForDelete()
    {
        await EnrollUserInAuthenticator();

        var getResponse = await _client.PostAsJsonAsync("/two-factor/get-authenticator",
            new { MasterPasswordHash = MasterPasswordHash });
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var root = await ReadJsonRootAsync(getResponse);
        var authenticator = root.GetProperty("authenticator");
        Assert.True(authenticator.GetProperty("enabled").GetBoolean());
        var key = authenticator.GetProperty("key").GetString()!;
        var uvToken = root.GetProperty("userVerificationToken").GetString()!;

        var disableResponse = await SendJsonAsync(_client, HttpMethod.Delete, "/two-factor/authenticator",
            new TwoFactorAuthenticatorDeleteRequestModel
            {
                Key = key,
                UserVerificationToken = uvToken,
            });
        Assert.Equal(HttpStatusCode.NoContent, disableResponse.StatusCode);

        var refreshed = await _userRepository.GetByEmailAsync(_userEmail);
        Assert.Null(refreshed!.GetTwoFactorProvider(TwoFactorProviderType.Authenticator));
    }

    [Fact]
    public async Task PutAuthenticator_ValidTokenAndCode_UpdatesProvider()
    {
        // GET mints a fresh key + UV token; compute the TOTP off that key and PUT.
        // AuthenticatorTokenProvider is verify-only (GenerateAsync returns null); compute the
        // TOTP with OtpNet, which is the same library the provider validates against.
        var getResponse = await _client.PostAsJsonAsync("/two-factor/get-authenticator",
            new { MasterPasswordHash = MasterPasswordHash });
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var root = await ReadJsonRootAsync(getResponse);
        var key = root.GetProperty("authenticator").GetProperty("key").GetString()!;
        var uvToken = root.GetProperty("userVerificationToken").GetString()!;
        var totp = new Totp(Base32Encoding.ToBytes(key)).ComputeTotp();

        var response = await _client.PutAsJsonAsync("/two-factor/authenticator",
            new TwoFactorAuthenticatorUpdateRequestModel
            {
                Token = totp,
                Key = key,
                UserVerificationToken = uvToken,
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var refreshed = await _userRepository.GetByEmailAsync(_userEmail);
        Assert.NotNull(refreshed!.GetTwoFactorProvider(TwoFactorProviderType.Authenticator));
    }

    [Fact]
    public async Task DeleteAuthenticator_BodyTypeMismatch_RespectsUrlRoute()
    {
        // Any "Type" field sent in the wire body must be ignored — the URL determines the
        // provider. Enroll Authenticator and Email, then send a body claiming Type=Email
        // to /authenticator and assert only Authenticator is touched.
        await EnrollUserInAuthenticator();
        await EnrollUserInEmail();

        var user = (await _userRepository.GetByEmailAsync(_userEmail))!;
        var authToken = _authenticatorTokenFactory.Protect(
            new TwoFactorAuthenticatorUserVerificationTokenable(user, AuthenticatorKey));

        var body = new JsonObject
        {
            ["Type"] = (int)TwoFactorProviderType.Email,
            ["Key"] = AuthenticatorKey,
            ["UserVerificationToken"] = authToken,
        };

        var response = await SendRawJsonAsync(_client, HttpMethod.Delete, "/two-factor/authenticator", body.ToJsonString());
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var refreshed = (await _userRepository.GetByEmailAsync(_userEmail))!;
        Assert.Null(refreshed.GetTwoFactorProvider(TwoFactorProviderType.Authenticator));
        Assert.NotNull(refreshed.GetTwoFactorProvider(TwoFactorProviderType.Email));
    }

    private Task EnrollUserInAuthenticator() =>
        SetUserTwoFactorProvidersJsonAsync(
            _userRepository, _userEmail, BuildAuthenticatorProvidersJson(AuthenticatorKey));

    private Task EnrollUserInEmail() =>
        SetUserTwoFactorProvidersJsonAsync(
            _userRepository, _userEmail, BuildEmailProvidersJson(_userEmail));
}
