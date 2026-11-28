using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.HttpExtensions;

namespace Bit.Identity.Models.Response.StaffSearch;

/// <summary>
/// An organization under no provider that the internal staff search found: what the Admin Portal's
/// add-existing-organization list shows of it (<c>ProvidersController.AddExistingOrganization</c>), and nothing else.
/// </summary>
public class StaffSearchUnassignedOrganizationResponseModel : ResponseModel
{
    public StaffSearchUnassignedOrganizationResponseModel(Organization organization)
        : base("staffSearchUnassignedOrganization")
    {
        ArgumentNullException.ThrowIfNull(organization);

        Id = organization.Id;
        Name = organization.DisplayName();
        PlanType = organization.PlanType;
    }

    public Guid Id { get; set; }

    /// <summary>
    /// The name as the list's model holds it: <see cref="Organization.DisplayName"/>, decoded once. The list's view
    /// decodes it again as it writes it.
    /// </summary>
    public string Name { get; set; }

    public PlanType PlanType { get; set; }
}
