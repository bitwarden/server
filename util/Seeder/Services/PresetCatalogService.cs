using Bit.Seeder.Models;

namespace Bit.Seeder.Services;

/// <summary>
/// Discovers available presets and fixtures from embedded seed resources.
/// </summary>
public static class PresetCatalogService
{
    public static AvailableSeeds ListAvailable()
    {
        var reader = new SeedReader();
        var all = reader.ListAvailable();

        var presets = all.Where(n => n.StartsWith("presets."))
            .Select(n => n["presets.".Length..])
            .ToList();

        var fixtures = all.Where(n => !n.StartsWith("presets."))
            .GroupBy(n => n.Split('.')[0])
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g.ToList());

        return new AvailableSeeds(presets, fixtures);
    }

    public static bool IsIndividualPreset(string presetName)
    {
        var reader = new SeedReader();
        var preset = reader.Read<SeedPreset>($"presets.{presetName}");
        return preset.IsIndividual;
    }
}

/// <summary>
/// Available presets and fixtures grouped by category.
/// </summary>
public record AvailableSeeds(
    IReadOnlyList<string> Presets,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Fixtures);
