using Bit.Core.AdminConsole.Enums;
using Bit.Core.Billing.Enums;
using Bit.Seeder.Factories;

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
            ValidateUsers(preset, presetName);
            ValidateDefaultCollections(preset, presetName);
        }
    }

    /// <summary>
    /// Default collections exist only because the Organization Data Ownership policy created them, and only on
    /// plans with My Items.
    /// </summary>
    /// <remarks>
    /// The preset's <c>organization.planType</c> is the effective plan for inline and fixture orgs alike:
    /// organization fixtures carry no plan, and a missing planType means EnterpriseAnnually.
    /// </remarks>
    private static void ValidateDefaultCollections(Models.SeedPreset preset, string presetName)
    {
        var defaultCollections = preset.DefaultCollections;
        if (defaultCollections is null)
        {
            return;
        }

        if (!ParsePolicyTypes(preset.Policies?.Enable, presetName).Contains(PolicyType.OrganizationDataOwnership))
        {
            throw new InvalidOperationException(
                $"Preset '{presetName}' has 'defaultCollections' but doesn't enable organizationDataOwnership, " +
                "the policy that creates default collections. Add it to 'policies.enable'.");
        }

        var planType = preset.Organization!.PlanType;
        if (PlanFeatures.Parse(planType) is not (PlanType.EnterpriseAnnually or PlanType.EnterpriseMonthly))
        {
            throw new InvalidOperationException(
                $"Preset '{presetName}' has 'defaultCollections' on plan '{planType}', which has no My Items. " +
                "Use an enterprise plan.");
        }

        if (defaultCollections.NonEmptyRate is < 0 or > 1)
        {
            throw new InvalidOperationException(
                $"Preset '{presetName}' has 'defaultCollections.nonEmptyRate' {defaultCollections.NonEmptyRate}; " +
                "it must be between 0 and 1.");
        }

        if (defaultCollections.DeletedRate is < 0)
        {
            throw new InvalidOperationException(
                $"Preset '{presetName}' has 'defaultCollections.deletedRate' {defaultCollections.DeletedRate}; " +
                "it must be 0 or more.");
        }

        var tooSmall = (defaultCollections.Largest ?? []).Where(n => n < 1).ToList();
        if (tooSmall.Count > 0)
        {
            throw new InvalidOperationException(
                $"Preset '{presetName}' has 'defaultCollections.largest' entries {string.Join(", ", tooSmall)}; " +
                "every entry must be at least 1.");
        }
    }

    private static void ValidateUsers(Models.SeedPreset preset, string presetName)
    {
        var users = preset.Users;
        if (users is null)
        {
            return;
        }

        if (users.StatusMix is not null)
        {
            if (users.RealisticStatusMix)
            {
                throw new InvalidOperationException(
                    $"Preset '{presetName}' sets both 'users.statusMix' and 'users.realisticStatusMix'. " +
                    "Use one: 'statusMix' gives explicit weights, 'realisticStatusMix' the built-in 85/5/5/5 mix.");
            }

            ValidateStatusMixWeights(users.StatusMix, presetName);
        }

        if (users.AdminCount < 0 || users.AdminCount > users.Count)
        {
            throw new InvalidOperationException(
                $"Preset '{presetName}' has 'users.adminCount' {users.AdminCount}; " +
                $"it must be between 0 and 'users.count' ({users.Count}).");
        }
    }

    /// <summary>
    /// Rejects weights that would make the normalized distribution invalid: a negative weight gives a negative
    /// bucket count, and an all-zero mix divides by zero.
    /// </summary>
    private static void ValidateStatusMixWeights(Models.SeedPresetStatusMix mix, string presetName)
    {
        (string Field, double Weight)[] weights =
        [
            ("confirmed", mix.Confirmed),
            ("invited", mix.Invited),
            ("accepted", mix.Accepted),
            ("revoked", mix.Revoked),
        ];

        var negative = weights.Where(w => w.Weight < 0).Select(w => w.Field).ToList();
        if (negative.Count > 0)
        {
            throw new InvalidOperationException(
                $"Preset '{presetName}' has 'users.statusMix' with a negative weight for {string.Join(", ", negative)}. " +
                "Weights must be 0 or more.");
        }

        if (weights.All(w => w.Weight <= 0))
        {
            throw new InvalidOperationException(
                $"Preset '{presetName}' has 'users.statusMix' with no positive weight. " +
                "Give at least one of confirmed, invited, accepted, revoked a weight above 0.");
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

        if (preset.DefaultCollections is not null)
        {
            invalidFields.Add("defaultCollections");
        }

        if (invalidFields.Count > 0)
        {
            throw new InvalidOperationException(
                $"Preset '{presetName}' is an individual user preset but contains " +
                $"organization-only fields: {string.Join(", ", invalidFields)}.");
        }
    }
}
