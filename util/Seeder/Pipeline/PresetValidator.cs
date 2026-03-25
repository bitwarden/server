namespace Bit.Seeder.Pipeline;

/// <summary>
/// Validates that a preset targets exactly one of organization or individual user.
/// </summary>
internal static class PresetValidator
{
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
            ValidateNoOrganizationFields(preset, presetName);
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
