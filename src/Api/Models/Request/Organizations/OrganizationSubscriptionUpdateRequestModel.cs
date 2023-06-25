using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;
using Bit.Core.Models.Business;

namespace Bit.Api.Models.Request.Organizations;

public class OrganizationSubscriptionUpdateRequestModel
{
    [Required]
    public int SeatAdjustment { get; set; }
    public int? MaxAutoscaleSeats { get; set; }

    public int? ServiceAccountsAdjustment { get; set; }
    public int? MaxAutoscaleServiceAccounts { get; set; }
    [Required]
    public BitwardenProductType BitwardenProduct { get; set; }

    public virtual OrganizationUpdate ToOrganizationUpdate(Guid orgIdGuid)
    {
        var orgUpdate = new OrganizationUpdate
        {
            OrganizationId = orgIdGuid,
            SeatAdjustment = SeatAdjustment,
            MaxAutoscaleSeats = MaxAutoscaleSeats,
            ServiceAccountsAdjustment = ServiceAccountsAdjustment.GetValueOrDefault(),
            MaxAutoscaleServiceAccounts = MaxAutoscaleServiceAccounts.GetValueOrDefault(),
            BitwardenProduct = BitwardenProduct
        };

        return orgUpdate;
    }
}
