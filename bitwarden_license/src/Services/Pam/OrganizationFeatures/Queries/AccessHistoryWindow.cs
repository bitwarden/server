namespace Bit.Services.Pam.OrganizationFeatures.Queries;

/// <summary>
/// The retention window every PAM history read goes through.
/// </summary>
public static class AccessHistoryWindow
{
    /// <summary>
    /// How far back a history read reaches.
    /// </summary>
    public const int RetentionDays = 90;
}
