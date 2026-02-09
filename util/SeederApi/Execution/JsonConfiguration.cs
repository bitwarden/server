using System.Text.Json;

namespace Bit.SeederApi.Execution;

/// <summary>
/// Provides shared JSON serialization configuration for executors.
/// </summary>
internal static class JsonConfiguration
{
    /// <summary>
    /// Standard JSON serializer options used for deserializing scene and query request models.
    /// Uses case-insensitive property matching and camelCase naming policy.
    /// </summary>
    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
