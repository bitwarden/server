using System.Text.Json;
using Bit.Core.Dirt.Reports.Models.Data;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace Bit.Core.Dirt.Reports.ReportFeatures;

public class GetPasskeyDirectoryQuery(
    IHttpClientFactory httpClientFactory,
    [FromKeyedServices(GetPasskeyDirectoryQuery.CacheName)]
    IFusionCache cache,
    ILogger<GetPasskeyDirectoryQuery> logger)
    : IGetPasskeyDirectoryQuery
{
    public const string HttpClientName = "PasskeyDirectoryHttpClient";
    public const string CacheName = "PasskeyDirectory";
    public static readonly TimeSpan CacheDuration = TimeSpan.FromDays(1);

    private const string _cacheKey = "passkey-directory";
    private const string _passkeyDirectoryUrl = "https://passkeys-api.2fa.directory/v1/all.json";

    private readonly HttpClient _httpClient = httpClientFactory.CreateClient(HttpClientName);

    public async Task<IEnumerable<PasskeyDirectoryEntry>> GetPasskeyDirectoryAsync()
    {
        var entries = await cache.GetOrSetAsync(
            key: _cacheKey,
            factory: async _ => await FetchPasskeyDirectoryAsync(),
            options: new FusionCacheEntryOptions(duration: CacheDuration)
        );

        return entries;
    }

    private async Task<List<PasskeyDirectoryEntry>> FetchPasskeyDirectoryAsync()
    {
        logger.LogInformation(Constants.BypassFiltersEventId,
            "Fetching passkey directory from external API");

        var response = await _httpClient.GetAsync(_passkeyDirectoryUrl);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync();
        var directory = await JsonSerializer.DeserializeAsync<Dictionary<string, JsonElement>>(stream);

        if (directory is null)
        {
            return [];
        }

        var entries = new List<PasskeyDirectoryEntry>();

        foreach (var (domain, serviceData) in directory)
        {
            var hasPasswordless = serviceData.TryGetProperty("passwordless", out var passwordlessElement)
                && passwordlessElement.ValueKind == JsonValueKind.String;
            var hasMfa = serviceData.TryGetProperty("mfa", out var mfaElement)
                && mfaElement.ValueKind == JsonValueKind.String;

            if (!hasPasswordless && !hasMfa)
            {
                continue;
            }

            var instructions = serviceData.TryGetProperty("documentation", out var docElement)
                && docElement.ValueKind == JsonValueKind.String
                ? docElement.GetString() ?? string.Empty
                : string.Empty;

            entries.Add(new PasskeyDirectoryEntry
            {
                DomainName = domain,
                Passwordless = hasPasswordless,
                Mfa = hasMfa,
                Instructions = instructions
            });
        }

        logger.LogInformation(Constants.BypassFiltersEventId,
            "Fetched {Count} passkey directory entries from external API", entries.Count);

        return entries;
    }
}
