namespace Bit.Core.Dirt.Enums;

public enum OrganizationIntegrationStatus : int
{
    NotApplicable,
    Invalid,
    Initiated,
    InProgress,
    Completed,

    /// <summary>
    /// The integration was fully configured but the remote end has since been torn down (for example, the
    /// Microsoft Teams app was uninstalled), so events can no longer be delivered until the owner reconnects.
    /// </summary>
    NeedsReconnection
}
