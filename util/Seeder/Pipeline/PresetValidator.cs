using Bit.Core.AdminConsole.Enums;
using Bit.Core.Billing.Enums;
using Bit.Seeder.Factories;

namespace Bit.Seeder.Pipeline;

/// <summary>
/// Validates that a preset targets exactly one of organization or individual user, and that organization presets
/// don't ask for settings that would be ignored or for states the server can't reach.
/// </summary>
internal static class PresetValidator
{
    // Mirrors the server's RequiredPolicies: these policies can't be enabled without Single Organization
    private static readonly PolicyType[] _requiresSingleOrg =
    [
        PolicyType.RequireSso, PolicyType.ResetPassword, PolicyType.MaximumVaultTimeout,
        PolicyType.AutomaticUserConfirmation, PolicyType.FillAssist, PolicyType.OrganizationUserNotification,
        PolicyType.UriMatchDefaults,
    ];

    internal static void Validate(Models.SeedPreset preset, string presetName)
    {
        var hasOrg = preset.Organization is not null;
        var hasUser = preset.User is not null;

        if (hasOrg && hasUser)
        {
            throw new InvalidOperationException(
                $"Preset '{presetName}' has both 'organization' and 'user'. " +
                "A preset must be one or the other.");
        }

        if (!hasOrg && !hasUser)
        {
            throw new InvalidOperationException(
                $"Preset '{presetName}' has neither 'organization' nor 'user'. " +
                "Every preset must specify exactly one.");
        }

        if (hasUser)
        {
            ValidateIndividualFields(preset, presetName);
            ValidateNoOrganizationFields(preset, presetName);
        }
        else
        {
            ValidateAccessShape(preset, presetName);
            ValidatePolicies(preset, presetName);
            ValidateMyItems(preset, presetName);
        }
    }

    /// <summary>
    /// <c>accessShape</c> replaces the density group, collection and assignment algorithms. Density settings it
    /// would silently ignore are rejected; cipher types, permissions, the orphan/archive/delete rates, personal
    /// ciphers and folders still apply.
    /// </summary>
    private static void ValidateAccessShape(Models.SeedPreset preset, string presetName)
    {
        var shape = preset.AccessShape;
        if (shape is null)
        {
            return;
        }

        if (preset.Groups is not null && shape.Groups is null)
        {
            throw new InvalidOperationException(
                $"Preset '{presetName}' has 'groups' and 'accessShape' but no 'accessShape.groups'. " +
                "Add an 'accessShape.groups' block, or drop 'accessShape' to use density groups.");
        }

        var density = preset.Density;
        var ignored = new List<string>();
        if (density?.Membership is not null)
        {
            ignored.Add("density.membership");
        }
        if (density?.CollectionFanOut is not null)
        {
            ignored.Add("density.collectionFanOut");
        }
        if (density?.DirectAccessRatio is not null)
        {
            ignored.Add("density.directAccessRatio");
        }
        if (density?.UserCollections is not null)
        {
            ignored.Add("density.userCollections");
        }
        if (density?.CipherAssignment?.Skew is not null)
        {
            ignored.Add("density.cipherAssignment.skew");
        }
        if (density?.CipherAssignment?.MultiCollectionRate is not null)
        {
            ignored.Add("density.cipherAssignment.multiCollectionRate");
        }
        if (density?.CipherAssignment?.MaxCollectionsPerCipher is not null)
        {
            ignored.Add("density.cipherAssignment.maxCollectionsPerCipher");
        }

        if (ignored.Count > 0)
        {
            throw new InvalidOperationException(
                $"Preset '{presetName}' uses 'accessShape', which replaces {string.Join(", ", ignored)}. " +
                "Remove them, or set the equivalent under 'accessShape'.");
        }
    }

    private static void ValidatePolicies(Models.SeedPreset preset, string presetName)
    {
        var policies = preset.Policies;
        if (policies is null)
        {
            return;
        }

        if (policies.EnableAll is not null || policies.Except is not null)
        {
            throw new InvalidOperationException(
                $"Preset '{presetName}' uses 'policies.enableAll'/'except', which are not supported. " +
                "List the policies in 'policies.enable' instead.");
        }

        var enabled = ParsePolicyTypes(policies.Enable, presetName);
        var disabled = ParsePolicyTypes(policies.Disable, presetName);

        var both = enabled.Intersect(disabled).ToList();
        if (both.Count > 0)
        {
            throw new InvalidOperationException(
                $"Preset '{presetName}' lists {string.Join(", ", both)} in both 'policies.enable' and 'policies.disable'.");
        }

        var orphanData = ParsePolicyTypes(policies.Data?.Keys, presetName).Except(enabled).Except(disabled).ToList();
        if (orphanData.Count > 0)
        {
            throw new InvalidOperationException(
                $"Preset '{presetName}' has 'policies.data' for {string.Join(", ", orphanData)}, " +
                "which is in neither 'policies.enable' nor 'policies.disable'.");
        }

        var missingSingleOrg = enabled.Intersect(_requiresSingleOrg).ToList();
        if (missingSingleOrg.Count > 0 && !enabled.Contains(PolicyType.SingleOrg))
        {
            throw new InvalidOperationException(
                $"Preset '{presetName}' enables {string.Join(", ", missingSingleOrg)} without singleOrg. " +
                "The server requires the Single Organization policy for these; add 'singleOrg' to 'policies.enable'.");
        }
    }

    /// <summary>
    /// My Items collections only exist because the Organization Data Ownership policy created them, and only on
    /// plans with My Items.
    /// </summary>
    private static void ValidateMyItems(Models.SeedPreset preset, string presetName)
    {
        if (preset.MyItems is null)
        {
            return;
        }

        if (!ParsePolicyTypes(preset.Policies?.Enable, presetName).Contains(PolicyType.OrganizationDataOwnership))
        {
            throw new InvalidOperationException(
                $"Preset '{presetName}' has 'myItems' but doesn't enable organizationDataOwnership, " +
                "the policy that creates My Items. Add it to 'policies.enable'.");
        }

        if (PlanFeatures.Parse(preset.Organization!.PlanType) is not (PlanType.EnterpriseAnnually or PlanType.EnterpriseMonthly))
        {
            throw new InvalidOperationException(
                $"Preset '{presetName}' has 'myItems' on plan '{preset.Organization.PlanType}', which has no My Items. " +
                "Use an enterprise plan.");
        }
    }

    private static HashSet<PolicyType> ParsePolicyTypes(IEnumerable<string>? names, string presetName) =>
        (names ?? []).Select(name => Enum.TryParse<PolicyType>(name, ignoreCase: true, out var type)
                ? type
                : throw new InvalidOperationException(
                    $"Preset '{presetName}' has unknown policy '{name}'. Valid values: {string.Join(", ", Enum.GetNames<PolicyType>())}."))
            .ToHashSet();

    private static void ValidateIndividualFields(Models.SeedPreset preset, string presetName)
    {
        var email = preset.User!.Email;
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            throw new InvalidOperationException(
                $"Preset '{presetName}' has an invalid user email: '{email}'. " +
                "Expected format: 'name@domain'.");
        }
    }

    private static void ValidateNoOrganizationFields(Models.SeedPreset preset, string presetName)
    {
        var invalidFields = new List<string>();

        if (preset.Roster is not null)
        {
            invalidFields.Add("roster");
        }

        if (preset.Users is not null)
        {
            invalidFields.Add("users");
        }

        if (preset.Groups is not null)
        {
            invalidFields.Add("groups");
        }

        if (preset.Collections is not null)
        {
            invalidFields.Add("collections");
        }

        if (preset.Density is not null)
        {
            invalidFields.Add("density");
        }

        if (preset.PersonalCiphers is not null)
        {
            invalidFields.Add("personalCiphers");
        }

        if (preset.CollectionAssignments is { Count: > 0 })
        {
            invalidFields.Add("collectionAssignments");
        }

        if (invalidFields.Count > 0)
        {
            throw new InvalidOperationException(
                $"Preset '{presetName}' is an individual user preset but contains " +
                $"organization-only fields: {string.Join(", ", invalidFields)}.");
        }
    }
}
