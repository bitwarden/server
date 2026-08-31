using Bit.Core.Exceptions;

namespace Bit.Services.Pam.OrganizationFeatures.Queries;

/// <summary>
/// The single retention window every PAM history surface reads through: the approver's resolved-request history, the
/// lease history, the audit trail, and the requester's own request history. It lives here rather than on any one of
/// those queries because it is the product's retention promise, not one view's implementation detail — when the reads
/// disagreed on it, the same resolved request was visible to its requester and invisible to its approver (PM-42614).
/// </summary>
public static class AccessHistoryWindow
{
    /// <summary>
    /// How far back a history read reaches. Older activity may be omitted. The audit trail pages within this window
    /// rather than returning all of it at once; the other history surfaces still read it whole.
    /// </summary>
    public const int RetentionDays = 90;

    /// <summary>
    /// The bounds a caller-supplied range resolves to, clamped to the window.
    ///
    /// An absent bound means "as far as the window allows", not "unbounded": the store holds no promise about anything
    /// older, so a caller asking for everything is asking for the window. An inverted pair is swapped rather than
    /// refused, matching <c>ApiHelpers.GetDateRange</c> on the organization event log. A span wider than the window is
    /// refused rather than clamped, so a caller asking for more history than exists is told so instead of being handed
    /// a narrower answer that looks like the one they asked for.
    ///
    /// It lives here, beside the retention constant, because the audit trail's page read and its item-filter read have
    /// to agree on it exactly — a menu built over a wider range than the page it filters would offer options the page
    /// can never match.
    /// </summary>
    /// <exception cref="BadRequestException">The requested span is wider than the retention window.</exception>
    public static (DateTime Since, DateTime Until) ResolveRange(DateTime? start, DateTime? end, DateTime now)
    {
        var retentionFloor = now.AddDays(-RetentionDays);
        var since = start ?? retentionFloor;
        var until = end ?? now;

        if (since > until)
        {
            (since, until) = (until, since);
        }

        if (until - since > TimeSpan.FromDays(RetentionDays))
        {
            throw new BadRequestException(
                $"The requested range is wider than the {RetentionDays}-day audit retention window.");
        }

        // The outer clamp: no parameter reaches past retention, whatever the span between the two bounds.
        return (since < retentionFloor ? retentionFloor : since, until);
    }
}
