using Bit.Seeder.Data.Distributions;
using Bit.Seeder.Data.Enums;
using Bit.Seeder.Factories;
using Bit.Seeder.Models;
using Bit.Seeder.Options;
using Bit.Seeder.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Seeder.Pipeline;

/// <summary>
/// Loads preset fixtures and registers them as recipes on <see cref="IServiceCollection"/>.
/// </summary>
internal static class PresetLoader
{
    /// <summary>
    /// Loads a preset from embedded fixtures and registers its steps as a recipe.
    /// </summary>
    /// <param name="presetName">Preset name without extension (e.g., "dunder-mifflin-full")</param>
    /// <param name="reader">Service for reading embedded seed JSON files</param>
    /// <param name="services">The service collection to register steps in</param>
    /// <exception cref="InvalidOperationException">Thrown when preset lacks organization configuration</exception>
    internal static void RegisterRecipe(string presetName, ISeedReader reader, IServiceCollection services)
    {
        var preset = reader.Read<SeedPreset>($"presets.{presetName}");

        if (preset.Organization is null)
        {
            throw new InvalidOperationException(
                $"Preset '{presetName}' must specify an organization.");
        }

        BuildRecipe(presetName, preset, reader, services);
    }

    /// <summary>
    /// Builds a recipe from preset configuration, resolving fixtures and generation counts.
    /// </summary>
    /// <remarks>
    /// Resolution order: Org → Roster → Owner (if no roster owner) → Generator → Users → Groups → Collections → Folders → Ciphers → PersonalCiphers
    /// </remarks>
    private static void BuildRecipe(string presetName, SeedPreset preset, ISeedReader reader, IServiceCollection services)
    {
        var builder = services.AddRecipe(presetName);
        var org = preset.Organization!;

        // Resolve domain - either from preset or from fixture
        var domain = org.Domain;

        if (org.Fixture is not null)
        {
            builder.UseOrganization(org.Fixture, org.PlanType, org.Seats);

            // If using a fixture and domain not explicitly provided, read it from the fixture
            if (domain is null)
            {
                var orgFixture = reader.Read<SeedOrganization>($"organizations.{org.Fixture}");
                domain = orgFixture.Domain;
            }
        }
        else if (org.Name is not null && org.Domain is not null)
        {
            var planType = PlanFeatures.Parse(org.PlanType);
            builder.CreateOrganization(org.Name, org.Domain, org.Seats, planType);
            domain = org.Domain;
        }

        if (preset.Roster?.Fixture is not null)
        {
            builder.UseRoster(preset.Roster.Fixture, reader);
        }

        if (!builder.HasRosterOwner)
        {
            builder.AddOwner();
        }

        // Generator requires a domain and is needed for generated ciphers, personal ciphers, or folders
        if (domain is not null && (preset.Ciphers?.Count > 0 || preset.PersonalCiphers?.CountPerUser > 0 || preset.Folders == true))
        {
            builder.WithGenerator(domain);
        }

        if (preset.Users is not null)
        {
            builder.AddUsers(preset.Users.Count, preset.Users.RealisticStatusMix);
        }

        var density = ParseDensity(preset.Density);

        if (preset.Groups is not null)
        {
            builder.AddGroups(preset.Groups.Count, density);
        }

        if (preset.Collections is not null)
        {
            builder.AddCollections(preset.Collections.Count, density);
        }

        if (preset.Folders == true)
        {
            builder.AddFolders();
        }

        if (preset.Ciphers?.Fixture is not null)
        {
            builder.UseCiphers(preset.Ciphers.Fixture);
        }
        else if (preset.Ciphers is not null && preset.Ciphers.Count > 0)
        {
            builder.AddCiphers(preset.Ciphers.Count, assignFolders: preset.Ciphers.AssignFolders, density: density);
        }

        if (preset.PersonalCiphers is not null && preset.PersonalCiphers.CountPerUser > 0)
        {
            builder.AddPersonalCiphers(preset.PersonalCiphers.CountPerUser);
        }

        builder.Validate();
    }

    private static DensityProfile? ParseDensity(SeedPresetDensity? preset)
    {
        if (preset is null)
        {
            return null;
        }

        return new DensityProfile
        {
            MembershipShape = ParseEnum(preset.Membership?.Shape, MembershipDistributionShape.Uniform),
            MembershipSkew = preset.Membership?.Skew ?? 0,
            CollectionFanOutMin = preset.CollectionFanOut?.Min ?? 1,
            CollectionFanOutMax = preset.CollectionFanOut?.Max ?? 3,
            FanOutShape = ParseEnum(preset.CollectionFanOut?.Shape, CollectionFanOutShape.Uniform),
            EmptyGroupRate = preset.CollectionFanOut?.EmptyGroupRate ?? 0,
            DirectAccessRatio = preset.DirectAccessRatio ?? 1.0,
            PermissionDistribution = ParsePermissions(preset.Permissions),
            CipherSkew = ParseEnum(preset.CipherAssignment?.Skew, CipherCollectionSkew.Uniform),
            OrphanCipherRate = preset.CipherAssignment?.OrphanRate ?? 0,
        };
    }

    private static Distribution<PermissionWeight> ParsePermissions(SeedPresetPermissions? permissions)
    {
        if (permissions is null)
        {
            return PermissionDistributions.Enterprise;
        }

        var readOnly = permissions.ReadOnly ?? 0;
        var readWrite = permissions.ReadWrite ?? 0;
        var manage = permissions.Manage ?? 0;
        var hidePasswords = permissions.HidePasswords ?? 0;

        // Empty permissions block (all nulls → zeros) — fall back to Enterprise defaults
        if (readOnly + readWrite + manage + hidePasswords < 0.001)
        {
            return PermissionDistributions.Enterprise;
        }

        return new Distribution<PermissionWeight>(
            (PermissionWeight.ReadOnly, readOnly),
            (PermissionWeight.ReadWrite, readWrite),
            (PermissionWeight.Manage, manage),
            (PermissionWeight.HidePasswords, hidePasswords));
    }

    private static T ParseEnum<T>(string? value, T defaultValue) where T : struct, Enum =>
        value is not null && Enum.TryParse<T>(value, ignoreCase: true, out var result) ? result : defaultValue;
}
