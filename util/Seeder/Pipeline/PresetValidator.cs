using Bit.Core.AdminConsole.Enums;

namespace Bit.Seeder.Pipeline;

/// <summary>
/// Validates that a preset targets exactly one of organization or individual user,
/// and rejects organization settings that would build an invalid or silently incomplete recipe.
/// </summary>
internal static class PresetValidator
{
    /// <summary>
    /// Policies whose server event handlers declare <c>RequiredPolicies => [PolicyType.SingleOrg]</c>:
    /// RequireSso, ResetPassword, MaximumVaultTimeout, UriMatchDefault, AutomaticUserConfirmation,
    /// OrganizationUserNotification and FillAssist PolicyEventHandler.
    /// </summary>
    private static readonly HashSet<PolicyType> _requiresSingleOrg =
    [
        PolicyType.RequireSso,
        PolicyType.ResetPassword,
        PolicyType.MaximumVaultTimeout,
        PolicyType.UriMatchDefaults,
        PolicyType.AutomaticUserConfirmation,
        PolicyType.OrganizationUserNotification,
        PolicyType.FillAssist,
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
            ValidatePolicies(preset, presetName);
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

        var enabled = ParseDistinctPolicyTypes(policies.Enable, "policies.enable", presetName);
        var disabled = ParseDistinctPolicyTypes(policies.Disable, "policies.disable", presetName);

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
    /// Parses one policy list and rejects repeated names, which would otherwise create two rows of one type
    /// and fail the (OrganizationId, Type) unique index at commit time.
    /// </summary>
    private static HashSet<PolicyType> ParseDistinctPolicyTypes(IEnumerable<string>? names, string field, string presetName)
    {
        var types = ParsePolicyTypes(names, presetName);
        var repeated = types.GroupBy(t => t).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (repeated.Count > 0)
        {
            throw new InvalidOperationException(
                $"Preset '{presetName}' lists {string.Join(", ", repeated)} more than once in '{field}'.");
        }

        return types.ToHashSet();
    }

    private static List<PolicyType> ParsePolicyTypes(IEnumerable<string>? names, string presetName) =>
        (names ?? []).Select(name => ParsePolicyType(name, presetName)).ToList();

    /// <summary>
    /// Matches the name case-insensitively against <see cref="PolicyType"/> member names only, so a numeric
    /// string such as "5" is rejected rather than parsed as a raw value.
    /// </summary>
    private static PolicyType ParsePolicyType(string name, string presetName)
    {
        var validNames = Enum.GetNames<PolicyType>();
        var match = validNames.FirstOrDefault(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            throw new InvalidOperationException(
                $"Preset '{presetName}' has unknown policy '{name}'. Valid values: {string.Join(", ", validNames)}.");
        }

        return Enum.Parse<PolicyType>(match);
    }

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

        if (preset.Policies is not null)
        {
            invalidFields.Add("policies");
        }

        if (invalidFields.Count > 0)
        {
            throw new InvalidOperationException(
                $"Preset '{presetName}' is an individual user preset but contains " +
                $"organization-only fields: {string.Join(", ", invalidFields)}.");
        }
    }
}
