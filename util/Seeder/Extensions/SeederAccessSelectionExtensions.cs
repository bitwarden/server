using Bit.Core.Models.Data;
using Bit.Seeder.Scenes;

namespace Bit.Seeder.Extensions;

internal static class SeederAccessSelectionExtensions
{
    public static List<CollectionAccessSelection>? DistinctOrThrowIfConflicting(
        this IEnumerable<CollectionAccessSelection>? selections, string subject)
    {
        if (selections == null)
        {
            return null;
        }

        var distinct = selections.DistinctOrThrowIfConflicting(
            s => s.Id,
            s => (s.ReadOnly, s.HidePasswords, s.Manage),
            id => $"Conflicting access settings requested for {subject} {id}.");

        var invalid = distinct.FirstOrDefault(s => !s.Valid());
        if (invalid != null)
        {
            throw new InvalidOperationException(
                $"Manage cannot be combined with ReadOnly or HidePasswords for {subject} {invalid.Id}.");
        }

        return distinct;
    }

    public static void ThrowIfAnyWithoutRead(this IEnumerable<OrganizationAccessPolicyScene.Grant> grants)
    {
        var unreadable = grants.FirstOrDefault(g => !g.Read);
        if (unreadable != null)
        {
            throw new InvalidOperationException(
                $"Grant for grantee {unreadable.GranteeId} on {unreadable.GrantableType} {unreadable.GrantableId} " +
                "must be Read = true.");
        }
    }
}
