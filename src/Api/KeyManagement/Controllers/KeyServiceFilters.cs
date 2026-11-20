namespace Bit.Api.KeyManagement.Controllers;

/// <summary>
/// Parses Key Service <c>filter[...]</c> query values, where several values are comma separated, e.g.
/// <c>filter[user]=62bed180-...,9c41e2a7-...</c>.
/// </summary>
public static class KeyServiceFilters
{
    /// <summary>
    /// Returns the values of a filter, or <c>null</c> when the filter is absent.
    /// </summary>
    public static IReadOnlyCollection<string>? Values(string? filter) =>
        filter?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// Returns the ids of a filter, or <c>null</c> when a value isn't an id.
    /// </summary>
    public static IReadOnlyCollection<Guid>? Ids(IReadOnlyCollection<string> values)
    {
        var ids = new List<Guid>();

        foreach (var value in values)
        {
            if (!Guid.TryParse(value, out var id))
            {
                return null;
            }

            ids.Add(id);
        }

        return ids;
    }
}
