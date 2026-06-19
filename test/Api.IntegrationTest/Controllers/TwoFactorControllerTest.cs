using System.Net;
using Bit.Api.Auth.Models.Request;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Entities;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Tokens;
using Xunit;

namespace Bit.Api.IntegrationTest.Controllers;

public class TwoFactorControllerTest : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private const string _key = "JBSWY3DPEHPK3PXP";

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private readonly IUserRepository _userRepository;
    private readonly IDataProtectorTokenFactory<TwoFactorAuthenticatorUserVerificationTokenable> _userVerificationTokenFactory;

    private string _userEmail = null!;

    public TwoFactorControllerTest(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.SubstituteService<IPushNotificationService>(_ => { });
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
        _userRepository = _factory.GetService<IUserRepository>();
        _userVerificationTokenFactory = _factory.GetService<IDataProtectorTokenFactory<TwoFactorAuthenticatorUserVerificationTokenable>>();
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

    /// <summary>
    /// Verifies the PUT /two-factor/authenticator call site composes its token
    /// guard correctly
    /// </summary>
    [Fact]
    public async Task PutAuthenticator_ExpiredToken_BadRequest()
    {
        var user = await _userRepository.GetByEmailAsync(_userEmail);
        Assert.NotNull(user);

        var expiredToken = ProtectExpiredUserVerificationToken(user);

        var requestModel = new UpdateTwoFactorAuthenticatorRequestModel
        {
            Token = "123456",
            Key = _key,
            UserVerificationToken = expiredToken,
        };

        using var message = new HttpRequestMessage(HttpMethod.Put, "/two-factor/authenticator");
        message.Content = JsonContent.Create(requestModel);
        var response = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("User verification failed.", content);

        // User must not have picked up an authenticator provider entry.
        var unchanged = await _userRepository.GetByEmailAsync(_userEmail);
        Assert.NotNull(unchanged);
        Assert.Null(unchanged.GetTwoFactorProvider(TwoFactorProviderType.Authenticator));
    }

    /// <summary>
    /// Verifies the DELETE /two-factor/authenticator call site composes its
    /// token guard correctly
    /// </summary>
    [Fact]
    public async Task DisableAuthenticator_ExpiredToken_BadRequest()
    {
        var user = await _userRepository.GetByEmailAsync(_userEmail);
        Assert.NotNull(user);

        var expiredToken = ProtectExpiredUserVerificationToken(user);

        var requestModel = new TwoFactorAuthenticatorDisableRequestModel
        {
            Key = _key,
            UserVerificationToken = expiredToken,
        };

        using var message = new HttpRequestMessage(HttpMethod.Delete, "/two-factor/authenticator");
        message.Content = JsonContent.Create(requestModel);
        var response = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("User verification failed.", content);
    }

    private string ProtectExpiredUserVerificationToken(User user)
    {
        var tokenable = new TwoFactorAuthenticatorUserVerificationTokenable(user, _key)
        {
            ExpirationDate = DateTime.UtcNow.AddMinutes(-1),
        };
        return _userVerificationTokenFactory.Protect(tokenable);
    }
}
