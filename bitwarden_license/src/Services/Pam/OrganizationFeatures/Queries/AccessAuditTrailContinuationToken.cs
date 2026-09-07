using System.Globalization;
using Bit.Pam.Models;

namespace Bit.Services.Pam.OrganizationFeatures.Queries;

/// <summary>
/// Where a page of the access-audit trail stopped, as the opaque string the client hands back to resume.
/// Carries the last row's instant AND its id, since events sharing a timestamp are routine in this store (an
/// action writes its before/after halves at one instant) and an instant-only token would silently drop the
/// others recorded at that instant.
/// </summary>
public static class AccessAuditTrailContinuationToken
{
    private const char Separator = '_';

    /// <summary>The token that resumes after <paramref name="lastRow"/>.</summary>
    public static string From(AccessAuditEvent lastRow) =>
        string.Create(CultureInfo.InvariantCulture, $"{lastRow.OccurredAt.Ticks}{Separator}{lastRow.Id:N}");

    /// <summary>
    /// Reads a token back into a position. False for anything this did not issue; a caller paging through the
    /// trail must not be silently restarted from the beginning.
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
