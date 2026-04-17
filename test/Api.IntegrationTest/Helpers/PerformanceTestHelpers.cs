using System.Net.Http.Headers;
using Bit.Api.IntegrationTest.Factories;

namespace Bit.Api.IntegrationTest.Helpers;

/// <summary>
/// Helper methods for performance tests to reduce code duplication.
/// </summary>
public static class PerformanceTestHelpers
{
    /// <summary>
    /// Standard password hash used across performance tests.
    /// </summary>
    public const string StandardPasswordHash = "c55hlJ/cfdvTd4awTXUqow6X3cOQCfGwn11o3HblnPs=";

    /// <summary>
    /// Authenticates an HttpClient with a bearer token for the specified user.
    /// </summary>
    /// <param name="factory">The application factory to use for login.</param>
    /// <param name="client">The HttpClient to authenticate.</param>
    /// <param name="email">The user's email address.</param>
    /// <param name="masterPasswordHash">The user's master password hash. Defaults to StandardPasswordHash.</param>
    public static async Task AuthenticateClientAsync(
        SqlServerApiApplicationFactory factory,
        HttpClient client,
        string email,
        string? masterPasswordHash = null)
    {
        var tokens = await factory.LoginAsync(email, masterPasswordHash ?? StandardPasswordHash);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);
    }
}
