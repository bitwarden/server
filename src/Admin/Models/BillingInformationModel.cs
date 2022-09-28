using Bit.Core.Models.Business;

namespace Bit.Admin.Models;

public class BillingInformationModel
{
    public BillingInfo BillingInfo { get; set; }
    public Guid? UserId { get; set; }
    public Guid? OrganizationId { get; set; }
}
