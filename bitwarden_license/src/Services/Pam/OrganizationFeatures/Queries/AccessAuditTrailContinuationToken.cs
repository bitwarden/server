using System.Globalization;
using Bit.Pam.Models;

namespace Bit.Services.Pam.OrganizationFeatures.Queries;

/// <summary>
/// Where a page of the access-audit trail stopped, as the opaque string the client hands back to resume.
///
/// It carries the last row's instant AND its id, because the instant alone does not identify a row: an action writes
/// its before/after halves at one instant, so events sharing a timestamp are ordinary in this store rather than a
/// remote tie. A position keyed on the instant alone would have to resume strictly before it and would silently drop
/// every other row recorded at that same instant — on an audit trail, the one kind of loss that must not happen
/// quietly. (The organization event log's token is instant-only; this deliberately diverges from it.)
/// </summary>
public static class AccessAuditTrailContinuationToken
{
    private const char Separator = '_';

    /// <summary>The token that resumes after <paramref name="lastRow"/>.</summary>
    public static string From(AccessAuditEvent lastRow) =>
        string.Create(CultureInfo.InvariantCulture, $"{lastRow.OccurredAt.Ticks}{Separator}{lastRow.Id:N}");

    /// <summary>
    /// Reads a token back into a position. Returns false for anything this did not issue.
    ///
    /// A caller paging through the trail — the CSV export walks every page — must not be answered with the first page
    /// when it asked for the fifth: that would loop forever, or silently write a file of repeats. So a token that does
    /// not parse is rejected by the endpoint rather than treated as "start from the beginning".
    /// </summary>
    public static bool TryParse(string token, out DateTime occurredAt, out Guid id)
    {
        occurredAt = default;
        id = default;

        var separator = token.IndexOf(Separator);
        if (separator <= 0)
        {
            return false;
        }

        if (!long.TryParse(
                token.AsSpan(0, separator), NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
            || ticks < DateTime.MinValue.Ticks
            || ticks > DateTime.MaxValue.Ticks)
        {
            return false;
        }

        if (!Guid.TryParseExact(token.AsSpan(separator + 1), "N", out id))
        {
            return false;
        }

        occurredAt = new DateTime(ticks, DateTimeKind.Utc);
        return true;
    }
}
