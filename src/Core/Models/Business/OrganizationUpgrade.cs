using Bit.Core.Enums;

namespace Bit.Core.Models.Business;

public class OrganizationUpgrade
{
    public string BusinessName { get; set; }
    public PlanType Plan { get; set; }
    public int AdditionalSeats { get; set; }
    public short AdditionalStorageGb { get; set; }
    public bool PremiumAccessAddon { get; set; }
    public TaxInfo TaxInfo { get; set; }
    public string PublicKey { get; set; }
    public string PrivateKey { get; set; }
}
