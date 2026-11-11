namespace Bit.Seeder.Extensions;

internal static class SeederEnumerableExtensions
{
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
