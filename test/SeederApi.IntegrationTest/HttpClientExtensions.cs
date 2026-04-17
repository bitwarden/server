using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Bit.SeederApi.IntegrationTest;

public static class HttpClientExtensions
{
    /// <summary>
    /// Sends a POST request with JSON content and attaches the x-play-id header.
    /// </summary>
    /// <typeparam name="TValue">The type of the value to serialize.</typeparam>
    /// <param name="client">The HTTP client.</param>
    /// <param name="requestUri">The URI the request is sent to.</param>
    /// <param name="value">The value to serialize.</param>
    /// <param name="playId">The play ID to attach as x-play-id header.</param>
    /// <param name="options">Options to control the behavior during serialization.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    public static Task<HttpResponseMessage> PostAsJsonAsync<TValue>(
        this HttpClient client,
        [StringSyntax(StringSyntaxAttribute.Uri)] string? requestUri,
        TValue value,
        string playId,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);

        if (string.IsNullOrWhiteSpace(playId))
        {
            throw new ArgumentException("Play ID cannot be null or whitespace.", nameof(playId));
        }

        var content = JsonContent.Create(value, mediaType: null, options);
        content.Headers.Remove("x-play-id");
        content.Headers.Add("x-play-id", playId);

        return client.PostAsync(requestUri, content, cancellationToken);
    }
}
