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
    /// How far back a history read reaches. Older activity may be omitted. v1 has no pagination, so this is the only
    /// bound on what a history surface will show.
    /// </summary>
    public const int RetentionDays = 90;
}
