using System.Text.Json;
using Bit.Core.Tools.Models.Api.Response;
using Bit.Core.Tools.Queries.Interfaces;
using Bit.Core.Utilities;
using Microsoft.Extensions.Caching.Distributed;
using Bit.Core.Exceptions;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Tools.Queries;

public class GetInactiveTwoFactorQuery : IGetInactiveTwoFactorQuery
{
    private const string _cacheKey = "ReportsInactiveTwoFactor";

    private readonly IDistributedCache _distributedCache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IGlobalSettings _globalSettings;
    private readonly ILogger<GetInactiveTwoFactorQuery> _logger;

    public GetInactiveTwoFactorQuery(
        IDistributedCache distributedCache,
        IHttpClientFactory httpClientFactory,
        IGlobalSettings globalSettings,
        ILogger<GetInactiveTwoFactorQuery> logger)
    {
        _distributedCache = distributedCache;
        _httpClientFactory = httpClientFactory;
        _globalSettings = globalSettings;
        _logger = logger;
    }

    public async Task<Dictionary<string, string>> GetInactiveTwoFactorAsync()
    {
        _distributedCache.TryGetValue(_cacheKey, out Dictionary<string, string> services);
        if (services != null)
        {
            return services;
        }

        using var client = _httpClientFactory.CreateClient();
        var response =
            await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, _globalSettings.TwoFactorDirectory.Uri));
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogInformation("Request to 2fa.Directory was unsuccessful: {statusCode}", response.StatusCode);
            throw new BadRequestException();
        }

        services = new Dictionary<string, string>();
        var deserializedData = ParseTwoFactorDirectoryTotpResponse(await response.Content.ReadAsStringAsync());

        foreach (var service in deserializedData.Where(service => !string.IsNullOrEmpty(service.Documentation)))
        {
            if (service.AdditionalDomains != null)
            {
                foreach (var additionalDomain in service.AdditionalDomains)
                {
                    // TryAdd used to prevent duplicate keys
                    services.TryAdd(additionalDomain, service.Documentation);
                }
            }

            // TryAdd used to prevent duplicate keys
            services.TryAdd(service.Domain, service.Documentation);
        }

        await _distributedCache.SetAsync(_cacheKey, services,
            new DistributedCacheEntryOptions().SetAbsoluteExpiration(
                new TimeSpan(_globalSettings.TwoFactorDirectory.CacheExpirationHours, 0, 0)));
        return services;
    }

    private static IEnumerable<TwoFactorDirectoryTotpResponseModel> ParseTwoFactorDirectoryTotpResponse(string json)
    {
        var data = new List<TwoFactorDirectoryTotpResponseModel>();
        using var jsonDocument = JsonDocument.Parse(json);
        // JSON response object opens with Array notation
        if (jsonDocument.RootElement.ValueKind == JsonValueKind.Array)
        {
            // Each nested array has two values: a floating "name" value [index: 0] and an object with desired data [index: 1]
            data.AddRange(from element in jsonDocument.RootElement.EnumerateArray()
                where element.ValueKind == JsonValueKind.Array && element.GetArrayLength() == 2
                select element[1].Deserialize<TwoFactorDirectoryTotpResponseModel>());
        }

        return data;
    }
}
