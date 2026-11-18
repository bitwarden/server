using Bit.Core.AdminConsole.Entities;
using Bit.HttpExtensions;

namespace Bit.Identity.Models.Response.StaffSearch;

/// <summary>
/// An organization the internal staff search found: what the Admin Portal's organizations index shows
/// of it, and nothing else (no keys, gateway identifiers or licence).
/// </summary>
public class StaffSearchOrganizationResponseModel : ResponseModel
{
    private const double BytesPerGb = 1073741824D;

    public StaffSearchOrganizationResponseModel(Organization organization)
        : base("staffSearchOrganization")
    {
        ArgumentNullException.ThrowIfNull(organization);

        Id = organization.Id;
        Name = organization.Name;
        Plan = organization.Plan;
        Seats = organization.Seats;
        CreationDate = organization.CreationDate;
        Paid = !string.IsNullOrWhiteSpace(organization.GatewaySubscriptionId);
        Enabled = organization.Enabled;
        ExpirationDate = organization.ExpirationDate;
        TwoFactorEnabled = organization.TwoFactorIsEnabled();
        StorageGb = organization.Storage.HasValue ? Math.Round(organization.Storage.Value / BytesPerGb, 2) : 0;
    }

    public Guid Id { get; set; }

    /// <summary>The name as this server stores it (HTML-encoded), as its other APIs answer it; <see cref="Organization.DisplayName"/> decodes it.</summary>
    public string Name { get; set; }

    public string Plan { get; set; }
    public int? Seats { get; set; }
    public DateTime CreationDate { get; set; }

    /// <summary>Whether a gateway subscription exists: the index's "Paid" or "Freeloader".</summary>
    public bool Paid { get; set; }

    public bool Enabled { get; set; }
    public DateTime? ExpirationDate { get; set; }

    /// <summary>Whether the organization has two-step login enabled (<see cref="Organization.TwoFactorIsEnabled"/>).</summary>
    public bool TwoFactorEnabled { get; set; }

    /// <summary>The storage used, in GB, rounded to two places as the index shows it.</summary>
    public double StorageGb { get; set; }
}
