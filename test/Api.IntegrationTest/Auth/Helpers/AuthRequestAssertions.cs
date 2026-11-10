using System.Net;
using System.Text.Json;
using Xunit;

namespace Bit.Api.IntegrationTest.Auth.Helpers;

/// <summary>
/// Assert helpers for auth request integration tests.
/// </summary>
internal static class AuthRequestAssertions
{
    public static async Task AssertBadRequestAsync(HttpResponseMessage response, string expectedMessage)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains(expectedMessage, content);
    }

    // AuthRequestResponseModel/PendingAuthRequestResponseModel only expose a parameterized constructor (they're
    // built from an AuthRequest entity), so System.Text.Json can't deserialize directly into them. Parse the raw
    // JSON instead, matching the convention already used by ResourceOwnerPasswordValidatorTests.
    public static async Task<JsonElement> AssertOkAndGetJsonAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement;
    }
}
