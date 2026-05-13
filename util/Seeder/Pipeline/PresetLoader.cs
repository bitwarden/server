using Bit.Core.Vault.Enums;
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
        PresetValidator.Validate(preset, presetName);

        if (preset.IsIndividual)
        {
            BuildIndividualRecipe(presetName, preset, services);
        }
        else
        {
            BuildRecipe(presetName, preset, reader, services);
        }
    }

    private static void BuildIndividualRecipe(string presetName, SeedPreset preset, IServiceCollection services)
    {
        var builder = services.AddRecipe(presetName);
        var user = preset.User!;
        var domain = user.Email.Split('@')[1];

        builder.CreateIndividualUser(user.Email, user.Premium, user.MaxStorageGb);

        if (preset.FolderNames is { Count: > 0 })
        {
            builder.AddNamedFolders(preset.FolderNames);
        }

        var needsGenerator = preset.Folders == true || preset.Ciphers is { Count: > 0 };

        if (needsGenerator)
        {
            builder.WithGenerator(domain);
        }

        if (preset.Folders == true)
        {
            builder.AddFolders(null);
        }

        if (preset.Ciphers?.Fixture is not null)
        {
            builder.UsePersonalVaultCiphers(preset.Ciphers.Fixture);
        }
        else if (preset.Ciphers is { Count: > 0 })
        {
            builder.AddPersonalCiphers(preset.Ciphers.Count, repromptEveryNthCipher: preset.Ciphers.RepromptEveryNthCipher);
        }

        if (preset.FolderAssignments is { Count: > 0 })
        {
            builder.CreateCipherFolders(preset.FolderAssignments);
        }

        if (preset.FavoriteAssignments is { Count: > 0 })
        {
            builder.CreateCipherFavorites(preset.FavoriteAssignments);
        }

        builder.Validate();
    }

    /// <summary>
    /// Builds a recipe from preset configuration, resolving fixtures and generation counts.
    /// </summary>
    /// <remarks>
    /// Resolution order: Org → OrgApiKey → Roster → Owner (if no roster owner) → Generator → Users → Groups → Collections → Folders → Ciphers → CipherCollections → CipherFolders → CipherFavorites → PersonalCiphers
    /// </remarks>
    private static void BuildRecipe(string presetName, SeedPreset preset, ISeedReader reader, IServiceCollection services)
    {
        var builder = services.AddRecipe(presetName);
        var org = preset.Organization!;

        // Resolve domain - either from preset or from fixture
        var domain = org.Domain;

        if (org.Fixture is not null)
        {
            builder.UseOrganization(org.Fixture, org.PlanType, org.Seats, ToOverrides(org));

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
            builder.CreateOrganization(org.Name, org.Domain, org.Seats, planType, ToOverrides(org));
            domain = org.Domain;
        }

        builder.AddOrganizationApiKey();

        if (preset.Roster?.Fixture is not null)
        {
            builder.UseRoster(preset.Roster.Fixture, reader);
        }

        if (!builder.HasRosterOwner)
        {
            builder.AddOwner();
        }

        var density = ParseDensity(preset.Density);

        // Generator requires a domain and is needed for generated ciphers, personal ciphers, or folders
        if (domain is not null && (
            preset.Ciphers?.Count > 0 ||
            preset.PersonalCiphers?.CountPerUser > 0 ||
            preset.Folders == true ||
            density?.FolderDistribution is not null ||
            density?.PersonalCipherDistribution is not null))
        {
            builder.WithGenerator(domain);
        }

        if (preset.Users is not null)
        {
            builder.AddUsers(preset.Users.Count, preset.Users.RealisticStatusMix);
        }

        if (preset.Groups is not null)
        {
            builder.AddGroups(preset.Groups.Count, density);
        }

        if (preset.Collections is not null)
        {
            builder.AddCollections(preset.Collections.Count, density);
        }

        if (preset.Folders == true || density?.FolderDistribution is not null)
        {
            builder.AddFolders(density);
        }

        var hasCollectionAssignments = preset.CollectionAssignments is { Count: > 0 };

        if (preset.Ciphers?.Fixture is not null)
        {
            builder.UseCiphers(preset.Ciphers.Fixture, skipCollectionAssignment: hasCollectionAssignments);
        }
        else if (preset.Ciphers is not null && preset.Ciphers.Count > 0)
        {
            builder.AddCiphers(preset.Ciphers.Count, assignFolders: preset.Ciphers.AssignFolders, density: density, repromptEveryNthCipher: preset.Ciphers.RepromptEveryNthCipher);
        }

        if (hasCollectionAssignments)
        {
            builder.CreateCipherCollections(preset.CollectionAssignments!);
        }

        if (preset.FolderAssignments is { Count: > 0 })
        {
            builder.CreateCipherFolders(preset.FolderAssignments);
        }

        if (preset.FavoriteAssignments is { Count: > 0 })
        {
            builder.CreateCipherFavorites(preset.FavoriteAssignments);
        }

        if (preset.PersonalCiphers is not null && preset.PersonalCiphers.CountPerUser > 0)
        {
            builder.AddPersonalCiphers(preset.PersonalCiphers.CountPerUser, density: density, repromptEveryNthCipher: preset.PersonalCiphers.RepromptEveryNthCipher);
        }
        else if (density?.PersonalCipherDistribution is not null)
        {
            builder.AddPersonalCiphers(0, density: density);
        }

        builder.Validate();
    }

    private static OrganizationOverrides ToOverrides(SeedPresetOrganization org) => new()
    {
        UseAutomaticUserConfirmation = org.UseAutomaticUserConfirmation,
        AllowAdminAccessToAllCollectionItems = org.AllowAdminAccessToAllCollectionItems,
        LimitItemDeletion = org.LimitItemDeletion,
        LimitCollectionCreation = org.LimitCollectionCreation,
        LimitCollectionDeletion = org.LimitCollectionDeletion,
    };

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
            MultiCollectionRate = preset.CipherAssignment?.MultiCollectionRate ?? 0,
            MaxCollectionsPerCipher = preset.CipherAssignment?.MaxCollectionsPerCipher ?? 2,
            UserCollectionMin = preset.UserCollections?.Min ?? 1,
            UserCollectionMax = preset.UserCollections?.Max ?? 3,
            UserCollectionShape = ParseEnum(preset.UserCollections?.Shape, CollectionFanOutShape.Uniform),
            UserCollectionSkew = preset.UserCollections?.Skew ?? 0,
            CipherTypeDistribution = ParseCipherTypes(preset.CipherTypes),
            PersonalCipherDistribution = ParsePersonalCipherDistribution(preset.PersonalCiphers?.Shape),
            FolderDistribution = ParseFolderDistribution(preset.Folders?.Shape),
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

    private static Distribution<CipherType>? ParseCipherTypes(SeedPresetCipherTypes? cipherTypes)
    {
        if (cipherTypes is null)
        {
            return null;
        }

        if (cipherTypes.Preset is not null)
        {
            return cipherTypes.Preset.ToLowerInvariant() switch
            {
                "realistic" => CipherTypeDistributions.Realistic,
                "loginonly" => CipherTypeDistributions.LoginOnly,
                "documentationheavy" => CipherTypeDistributions.DocumentationHeavy,
                "developerfocused" => CipherTypeDistributions.DeveloperFocused,
                _ => throw new InvalidOperationException(
                    $"Unknown cipher type preset '{cipherTypes.Preset}'. Valid values: realistic, loginOnly, documentationHeavy, developerFocused."),
            };
        }

        var login = cipherTypes.Login ?? 0;
        var secureNote = cipherTypes.SecureNote ?? 0;
        var card = cipherTypes.Card ?? 0;
        var identity = cipherTypes.Identity ?? 0;
        var sshKey = cipherTypes.SshKey ?? 0;

        return new Distribution<CipherType>(
            (CipherType.Login, login),
            (CipherType.SecureNote, secureNote),
            (CipherType.Card, card),
            (CipherType.Identity, identity),
            (CipherType.SSHKey, sshKey));
    }

    private static Distribution<(int Min, int Max)>? ParsePersonalCipherDistribution(string? shape)
    {
        if (shape is null)
        {
            return null;
        }

        return shape.ToLowerInvariant() switch
        {
            "realistic" => PersonalCipherDistributions.Realistic,
            "lightusage" => PersonalCipherDistributions.LightUsage,
            "heavyusage" => PersonalCipherDistributions.HeavyUsage,
            _ => throw new InvalidOperationException(
                $"Unknown personal cipher distribution '{shape}'. Valid values: realistic, lightUsage, heavyUsage."),
        };
    }

    private static Distribution<(int Min, int Max)>? ParseFolderDistribution(string? shape)
    {
        if (shape is null)
        {
            return null;
        }

        return shape.ToLowerInvariant() switch
        {
            "realistic" => FolderCountDistributions.Realistic,
            "enterprise" => FolderCountDistributions.Enterprise,
            "minimal" => FolderCountDistributions.Minimal,
            _ => throw new InvalidOperationException(
                $"Unknown folder distribution '{shape}'. Valid values: realistic, enterprise, minimal."),
        };
    }

    private static T ParseEnum<T>(string? value, T defaultValue) where T : struct, Enum
    {
        if (value is null)
        {
            return defaultValue;
        }

        if (!Enum.TryParse<T>(value, ignoreCase: true, out var result))
        {
            var valid = string.Join(", ", Enum.GetNames<T>());
            throw new InvalidOperationException(
                $"Unknown {typeof(T).Name} '{value}'. Valid values: {valid}.");
        }

        return result;
    }
}
