using System.Net;
using System.Text;
using Bit.SeederApi.Models.Request;
using Duende.IdentityModel.Client;
using Xunit;

namespace Bit.SeederApi.IntegrationTest;

public class BasicAuthenticationHandlerTests : IClassFixture<SeederApiApplicationFactory>, IAsyncLifetime
{
    private const string User1 = "user1";
    private const string Pass1 = "pass1";
    private const string User2 = "user2";
    private const string Pass2 = "pass2";
    private const string BlankPwUser = "blank-pw-user";
    private const string OrphanPassword = "orphan-password";
    private const string DupePass = "different-pass";

    private readonly HttpClient _client;

    public BasicAuthenticationHandlerTests(SeederApiApplicationFactory factory)
    {
        factory.ConfigureAccounts(
            (User1, Pass1),
            (User2, Pass2),
            (BlankPwUser, ""),
            ("", OrphanPassword),
            (User1, DupePass));
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    // Usability

    [Fact]
    public async Task ProtectedEndpoint_WithValidCredentials_ReturnsOk()
    {
        _client.SetBasicAuthentication(User1, Pass1);
        var response = await PostQuery();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithMissingAuthorizationHeader_Returns401()
    {
        var response = await PostQuery();
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("Bearer some-token")]
    [InlineData("Digest realm=\"x\"")]
    [InlineData("NotAScheme")]
    public async Task ProtectedEndpoint_WithNonBasicScheme_Returns401(string headerValue)
    {
        _client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", headerValue);
        var response = await PostQuery();
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithLowercaseBasicScheme_ReturnsOk()
    {
        // Convention: RFC 7617 auth-scheme is case-insensitive.
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{User1}:{Pass1}"));
        _client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"basic {token}");
        var response = await PostQuery();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithMalformedBase64_Returns401()
    {
        _client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Basic {User1}:{Pass1}");
        var response = await PostQuery();
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithEmptyBasicToken_Returns401()
    {
        _client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Basic ");
        var response = await PostQuery();
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithBase64ButNoColon_Returns401()
    {
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes("no-colon-here"));
        _client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Basic {token}");
        var response = await PostQuery();
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithMultipleAuthorizationHeaders_Returns401()
    {
        _client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization",
            new[] { $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Concat([User1, ":", Pass1])))}", $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Concat([User2, ":", Pass2])))}" });
        var response = await PostQuery();
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Security

    [Fact]
    public async Task ProtectedEndpoint_WithValidUsernameAndWrongPassword_Returns401()
    {
        _client.SetBasicAuthentication(User1, "wrong-password");
        var response = await PostQuery();
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithUnknownUsername_Returns401()
    {
        _client.SetBasicAuthentication("unknown-user", Pass1);
        var response = await PostQuery();
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithCrossAccountUsernameAndPassword_Returns401()
    {
        // Account A's username + Account B's password must not authenticate.
        _client.SetBasicAuthentication(User1, Pass2);
        var response = await PostQuery();
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithWrongUsernameCase_Returns401()
    {
        // Ordinal comparison — case must match exactly.
        _client.SetBasicAuthentication(User1.ToUpperInvariant(), Pass1);
        var response = await PostQuery();
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithEmptyUsername_Returns401()
    {
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($":{Pass1}"));
        _client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Basic {token}");
        var response = await PostQuery();
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithEmptyPassword_Returns401()
    {
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{User1}:"));
        _client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Basic {token}");
        var response = await PostQuery();
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Multi-account

    [Fact]
    public async Task ProtectedEndpoint_WithSecondAccountCredentials_ReturnsOk()
    {
        _client.SetBasicAuthentication(User2, Pass2);
        var response = await PostQuery();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task BlankConfiguredPassword_DoesNotAuthenticate_EvenWithEmptySuppliedPassword()
    {
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{BlankPwUser}:"));
        _client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Basic {token}");
        var response = await PostQuery();
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task BlankConfiguredUsername_DoesNotAuthenticate_EvenWithMatchingPassword()
    {
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($":{OrphanPassword}"));
        _client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Basic {token}");
        var response = await PostQuery();
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DuplicateConfiguredUsername_LaterOccurrencePasswordRejected()
    {
        // First-occurrence-wins is verified by ProtectedEndpoint_WithValidCredentials_ReturnsOk
        // (User1 is duplicated in the fixture with DupePass, and Pass1 still authenticates).
        _client.SetBasicAuthentication(User1, DupePass);
        var response = await PostQuery();
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AnonymousEndpoint_WithoutAuth_ReturnsOk()
    {
        var response = await _client.GetAsync("/alive");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AnonymousEndpoint_WithInvalidAuth_ReturnsOk()
    {
        _client.SetBasicAuthentication("nobody", "nothing");
        var response = await _client.GetAsync("/alive");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private Task<HttpResponseMessage> PostQuery()
    {
        return _client.PostAsJsonAsync("/query", new QueryRequestModel
        {
            Template = "EmergencyAccessInviteQuery",
            Arguments = System.Text.Json.JsonSerializer.SerializeToElement(new { email = "any@example.com" })
        });
    }
}

public class BasicAuthenticationHandlerUnconfiguredTests
    : IClassFixture<SeederApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;

    public BasicAuthenticationHandlerUnconfiguredTests(SeederApiApplicationFactory factory)
    {
        // Deliberately do NOT call ConfigureAccounts — the handler should reject every
        // protected request when no accounts are wired up.
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ProtectedEndpoint_WithNoAccountsConfigured_Returns401()
    {
        _client.SetBasicAuthentication("someone", "somepass");
        var response = await _client.PostAsJsonAsync("/query", new QueryRequestModel
        {
            Template = "EmergencyAccessInviteQuery",
            Arguments = System.Text.Json.JsonSerializer.SerializeToElement(new { email = "any@example.com" })
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AnonymousEndpoint_WithNoAccountsConfigured_ReturnsOk()
    {
        var response = await _client.GetAsync("/alive");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
