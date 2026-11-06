using System.Net;
using System.Net.Http.Json;
using Bit.Core.Platform.Mail.Delivery;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using RichardSzalay.MockHttp;

namespace Bit.MailDelivery.Test.SmtpOAuth;

public class ClientCredentialsHandlerTests
{
    private readonly MockHttpMessageHandler _mockHandler;
    private readonly ClientCredentialsHandler _sut;

    public ClientCredentialsHandlerTests()
    {
        _mockHandler = new MockHttpMessageHandler();
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("SmtpOAuth").Returns(_mockHandler.ToHttpClient());

        _sut = new ClientCredentialsHandler(
            httpClientFactory,
            new FakeTimeProvider(),
            Substitute.For<ILogger>(),
            "https://example.com/token",
            "test_username",
            "test_client_id",
            "test_client_secret",
            "test_scope");
    }

    [Fact]
    public async Task GetAsync_PostsExpectedFormFields()
    {
        _mockHandler
            .When(HttpMethod.Post, "https://example.com/token")
            .WithExactFormData(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = "test_client_id",
                ["client_secret"] = "test_client_secret",
                ["scope"] = "test_scope",
            })
            .Respond(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { access_token = "test_token", expires_in = 3599 }),
            });

        var credentials = await _sut.GetAsync(CancellationToken.None);

        Assert.Equal("XOAUTH2", credentials.MechanismName);
    }
}
