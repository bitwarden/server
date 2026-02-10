using System.Reflection;
using System.Text.Json;

namespace Bit.Seeder.Services;

/// <summary>
/// Reads seed data from embedded JSON resources shipped with the Seeder library.
/// </summary>
internal sealed class SeedReader : ISeedReader
{
    private const string _resourcePrefix = "Bit.Seeder.Seeds.fixtures.";
    private const string _resourceSuffix = ".json";

    private static readonly Assembly _assembly = typeof(SeedReader).Assembly;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public T Read<T>(string seedName)
    {
        var resourceName = $"{_resourcePrefix}{seedName}{_resourceSuffix}";
        using var stream = _assembly.GetManifestResourceStream(resourceName);

        if (stream is null)
        {
            var available = string.Join(", ", ListAvailable());
            throw new InvalidOperationException(
                $"Seed file '{seedName}' not found. Available seeds: {available}");
        }

        try
        {
            var result = JsonSerializer.Deserialize<T>(stream, _jsonOptions);

            if (result is null)
            {
                throw new InvalidOperationException(
                    $"Seed file '{seedName}' deserialized to null. The JSON may be empty or incompatible with type {typeof(T).Name}.");
            }

            return result;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Failed to deserialize seed file '{seedName}' as {typeof(T).Name}: {ex.Message}", ex);
        }
    }

    public IReadOnlyList<string> ListAvailable()
    {
        return _assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(_resourcePrefix) && n.EndsWith(_resourceSuffix))
            .Select(n => n[_resourcePrefix.Length..^_resourceSuffix.Length])
            .OrderBy(n => n)
            .ToList();
    }
}
