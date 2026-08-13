using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.Helpers;

namespace Bit.Billing.IntegrationTest;

/// <summary>
/// Test-host extensions that own the bits of identity flow our suite needs,
/// so this project doesn't reach back into the legacy IdentityApplicationFactory
/// helpers.
/// </summary>
public static class IdentityApplicationFactoryExtensions
{
    /// <summary>
    /// Exchanges a refresh token for a fresh access + refresh token pair via
    /// the Identity host's <c>/connect/token</c> endpoint.
    /// </summary>
    public static async Task<(string Token, string RefreshToken)> TokenFromRefreshAsync(
        this IdentityApplicationFactory identity,
        string refreshToken)
    {
        using var client = identity.CreateDefaultClient();
        var response = await client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "client_id", "web" },
            { "grant_type", "refresh_token" },
            { "refresh_token", refreshToken },
        }));

        await Assert.SuccessResponseAsync(response);

        var tokens = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        return (tokens["access_token"]!.GetValue<string>(), tokens["refresh_token"]!.GetValue<string>());
    }
}
