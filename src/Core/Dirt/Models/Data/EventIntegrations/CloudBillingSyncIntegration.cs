namespace Bit.Core.Dirt.Models.Data.EventIntegrations;

/// <summary>
/// Configuration for CloudBillingSync integration type.
/// </summary>
public class CloudBillingSyncIntegration
{
    /// <summary>
    /// Whether the billing sync integration is enabled.
    /// Replaces the separate Enabled column from OrganizationConnection.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// API key for cloud billing synchronization.
    /// </summary>
    public string BillingSyncKey { get; set; } = string.Empty;

    /// <summary>
    /// Reference to the cloud organization ID.
    /// </summary>
    public Guid CloudOrganizationId { get; set; }

    /// <summary>
    /// Timestamp of the last successful license sync.
    /// </summary>
    public DateTime? LastLicenseSync { get; set; }
}
