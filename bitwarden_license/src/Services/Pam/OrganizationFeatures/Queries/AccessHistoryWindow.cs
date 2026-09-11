using Bit.Core.Exceptions;

namespace Bit.Services.Pam.OrganizationFeatures.Queries;

/// <summary>
/// The single retention window every PAM history surface reads through: the approver's resolved-request history,
/// the lease history, the audit trail, and the requester's own request history.
/// </summary>
public static class AccessHistoryWindow
{
    /// <summary>
    /// How far back a history read reaches; older activity may be omitted. The audit trail pages within this
    /// window; the other history surfaces read it whole.
    /// </summary>
    public const int RetentionDays = 90;

    /// <summary>
    /// The bounds a caller-supplied range resolves to, clamped to the window. An absent bound means "as far as the
    /// window allows", not "unbounded". An inverted pair is swapped rather than refused, matching
    /// <c>ApiHelpers.GetDateRange</c> on the organization event log. A span wider than the window is refused
    /// rather than silently clamped.
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
