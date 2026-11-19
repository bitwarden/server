using Bit.Api.Auth.Models.Request.Accounts;

namespace Bit.Api.IntegrationTest.Auth.Helpers;

/// <summary>
/// Request helpers for the account email change endpoints.
/// </summary>
internal static class EmailChangeTestHelpers
{
    // Posts the self-service-shaped payload (no new master password / key) to the merged
    // /accounts/email endpoint. The same endpoint serves both paths; the feature flag picks
    // which command actually runs.
    public static async Task<HttpResponseMessage> PostEmailSelfServiceAsync(
        HttpClient client, string newEmail, string token, string masterPasswordHash)
    {
        var requestModel = new EmailRequestModel
        {
            MasterPasswordHash = masterPasswordHash,
            NewEmail = newEmail,
            Token = token
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, "/accounts/email");
        message.Content = JsonContent.Create(requestModel);
        return await client.SendAsync(message);
    }
}
