using Bit.Core.Enums;

namespace Bit.Core.Models.Business;

public class OrganizationUpdate
{
    public Guid OrganizationId { get; set; }
    public int SeatAdjustment { get; set; }
    public int? MaxAutoscaleSeats { get; set; }

    public int? ServiceAccountsAdjustment { get; set; }
    public int? MaxAutoscaleServiceAccounts { get; set; }
}
