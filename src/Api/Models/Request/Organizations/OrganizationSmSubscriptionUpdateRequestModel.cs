using System.ComponentModel.DataAnnotations;
using Bit.Core.Models.Business;

namespace Bit.Api.Models.Request.Organizations;

public class OrganizationSmSubscriptionUpdateRequestModel
{
    [Required]
    public int SeatAdjustment { get; set; }
    public int? MaxAutoscaleSeats { get; set; }

    public int? ServiceAccountAdjustment { get; set; }
    public int? MaxAutoscaleServiceAccounts { get; set; }

    public virtual OrganizationUpdate ToOrganizationUpdate(Guid orgIdGuid)
    {
        var orgUpdate = new OrganizationUpdate
        {
            OrganizationId = orgIdGuid,
            SeatAdjustment = SeatAdjustment,
            MaxAutoscaleSeats = MaxAutoscaleSeats,
            ServiceAccountsAdjustment = ServiceAccountAdjustment.GetValueOrDefault(),
            MaxAutoscaleServiceAccounts = MaxAutoscaleServiceAccounts.GetValueOrDefault()
        };

        return orgUpdate;
    }
}
