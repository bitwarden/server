using Bit.Seeder.Models;

namespace Bit.Seeder.Guards;

/// <summary>
/// Fails fast when a preset's fixed, golden organization ID already exists in the database,
/// producing an actionable error instead of a SQL primary-key violation from the BulkCommitter.
/// </summary>
internal static class FixedOrganizationIdGuard
{
    internal static void EnsureAvailable(SeedPresetOrganization? organization, Func<Guid, bool> organizationExists)
    {
        var id = ResolveFixedId(organization);
        if (id is null)
        {
            return;
        }

        if (organizationExists(id.Value))
        {
            throw new InvalidOperationException(
                $"Organization '{id}' already exists in the database. This preset seeds a fixed organization ID, " +
                "so it cannot be reseeded until the existing organization is removed. Delete it manually, or " +
                "restore your dev database, before rerunning this preset.");
        }
    }

    /// <summary>
    /// Parses a preset's declared organization ID, if any. Presets that omit <c>organization.id</c>
    /// get a fresh <see cref="Guid"/> generated at seed time and are never subject to this guard.
    /// </summary>
    internal static Guid? ResolveFixedId(SeedPresetOrganization? organization)
    {
        if (string.IsNullOrWhiteSpace(organization?.Id))
        {
            return null;
        }

        if (!Guid.TryParse(organization.Id, out var id))
        {
            throw new InvalidOperationException(
                $"Preset organization.id '{organization.Id}' is not a valid GUID.");
        }

        return id;
    }
}
