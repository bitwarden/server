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

        var distinct = selections
            .DistinctBy(s => (s.Id, s.ReadOnly, s.HidePasswords, s.Manage))
            .ToList();

        var conflicting = distinct.GroupBy(s => s.Id).FirstOrDefault(g => g.Count() > 1);
        if (conflicting != null)
        {
            throw new InvalidOperationException(
                $"Conflicting access settings requested for {subject} {conflicting.Key}.");
        }

        return distinct;
    }
}
