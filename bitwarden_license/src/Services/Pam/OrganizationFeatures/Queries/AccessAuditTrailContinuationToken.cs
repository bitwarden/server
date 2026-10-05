using System.Globalization;
using Bit.Pam.Models;

namespace Bit.Services.Pam.OrganizationFeatures.Queries;

/// <summary>
/// Where a page of the access-audit trail stopped, as an opaque string. Carries the last row's instant and id, since
/// events often share a timestamp.
/// </summary>
public static class AccessAuditTrailContinuationToken
{
    private const char Separator = '_';

    /// <summary>The token that resumes after <paramref name="lastRow"/>.</summary>
    public static string From(AccessAuditEvent lastRow) =>
        string.Create(CultureInfo.InvariantCulture, $"{lastRow.OccurredDate.Ticks}{Separator}{lastRow.Id:N}");

    /// <summary>Reads a token back into a position. False for anything this did not issue.</summary>
    public static bool TryParse(string token, out DateTime occurredDate, out Guid id)
    {
        occurredDate = default;
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

        occurredDate = new DateTime(ticks, DateTimeKind.Utc);
        return true;
    }
}
