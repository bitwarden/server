using Bit.Seeder.Models;
using Bit.Seeder.Services;

namespace Bit.Seeder.Pipeline;

/// <summary>
/// Loads preset fixtures and transforms them into configured RecipeBuilder instances.
/// </summary>
internal static class PresetLoader
{
    /// <summary>
    /// Loads a preset from embedded fixtures and builds a RecipeBuilder with configured steps.
    /// </summary>
    /// <param name="presetName">Preset name without extension (e.g., "dunder-mifflin-full")</param>
    /// <param name="reader">Service for reading embedded seed JSON files</param>
    /// <returns>Configured RecipeBuilder ready to build step list</returns>
    /// <exception cref="InvalidOperationException">Thrown when preset lacks organization configuration</exception>
    internal static RecipeBuilder Load(string presetName, ISeedReader reader)
    {
        var preset = reader.Read<SeedPreset>($"presets.{presetName}");

        if (preset.Organization is null)
        {
            throw new InvalidOperationException(
                $"Preset '{presetName}' must specify an organization.");
        }

        return BuildFromPreset(preset, reader);
    }

    /// <summary>
    /// Builds RecipeBuilder from preset configuration, resolving fixtures and generation counts.
    /// </summary>
    /// <remarks>
    /// Resolution order: Org → Owner → Generator → Roster → Users → Groups → Collections → Ciphers
    /// </remarks>
    private static RecipeBuilder BuildFromPreset(SeedPreset preset, ISeedReader reader)
    {
        var builder = new RecipeBuilder();
        var org = preset.Organization!;

        // Resolve domain - either from preset or from fixture
        var domain = org.Domain;

        if (org.Fixture is not null)
        {
            builder.UseOrganization(org.Fixture);

            // If using a fixture and domain not explicitly provided, read it from the fixture
            if (domain is null)
            {
                var orgFixture = reader.Read<SeedOrganization>($"organizations.{org.Fixture}");
                domain = orgFixture.Domain;
            }
        }
        else if (org.Name is not null && org.Domain is not null)
        {
            builder.CreateOrganization(org.Name, org.Domain, org.Seats);
            domain = org.Domain;
        }

        builder.AddOwner();

        // Generator requires a domain and is only needed for generated ciphers
        if (domain is not null && preset.Ciphers?.Count > 0)
        {
            builder.WithGenerator(domain);
        }

        if (preset.Roster?.Fixture is not null)
        {
            builder.UseRoster(preset.Roster.Fixture);
        }

        if (preset.Users is not null)
        {
            builder.AddUsers(preset.Users.Count, preset.Users.RealisticStatusMix);
        }

        if (preset.Groups is not null)
        {
            builder.AddGroups(preset.Groups.Count);
        }

        if (preset.Collections is not null)
        {
            builder.AddCollections(preset.Collections.Count);
        }

        if (preset.Ciphers?.Fixture is not null)
        {
            builder.UseCiphers(preset.Ciphers.Fixture);
        }
        else if (preset.Ciphers is not null && preset.Ciphers.Count > 0)
        {
            builder.AddCiphers(preset.Ciphers.Count);
        }

        return builder;
    }
}
