using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.Models.Business;

public class OrganizationSignup : OrganizationUpgrade
{
    public string Name { get; set; }
    public string BillingEmail { get; set; }
    public User Owner { get; set; }
    public string OwnerKey { get; set; }
    public string CollectionName { get; set; }
    public PaymentMethodType? PaymentMethodType { get; set; }
    public string PaymentToken { get; set; }
    public int? MaxAutoscaleSeats { get; set; } = null;
}
