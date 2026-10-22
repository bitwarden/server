using Bit.Core.Models.Data;

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

        return selections.DistinctOrThrowIfConflicting(
            s => s.Id,
            s => (s.ReadOnly, s.HidePasswords, s.Manage),
            id => $"Conflicting access settings requested for {subject} {id}.");
    }

    public static List<T> DistinctOrThrowIfConflicting<T, TId, TSettings>(
        this IEnumerable<T> items,
        Func<T, TId> id,
        Func<T, TSettings> settings,
        Func<TId, string> conflictMessage)
    {
        var distinct = items.DistinctBy(i => (id(i), settings(i))).ToList();

        var conflicting = distinct.GroupBy(id).FirstOrDefault(g => g.Count() > 1);
        if (conflicting != null)
        {
            throw new InvalidOperationException(conflictMessage(conflicting.Key));
        }

        return distinct;
    }
}
