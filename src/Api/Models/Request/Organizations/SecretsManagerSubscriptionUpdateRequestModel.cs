using System.ComponentModel.DataAnnotations;
using Bit.Core.Models.Business;

namespace Bit.Api.Models.Request.Organizations;

public class SecretsManagerSubscriptionUpdateRequestModel
{
    [Required]
    public int SeatAdjustment { get; set; }
    public int? MaxAutoscaleSeats { get; set; }

    public int ServiceAccountAdjustment { get; set; }
    public int? MaxAutoscaleServiceAccounts { get; set; }

    public virtual SecretsManagerSubscriptionUpdate ToOrganizationUpdate(Guid orgIdGuid)
    {
        var orgUpdate = new SecretsManagerSubscriptionUpdate
        {
            OrganizationId = orgIdGuid,
            SeatAdjustment = SeatAdjustment,
            MaxAutoscaleSeats = MaxAutoscaleSeats,
            ServiceAccountsAdjustment = ServiceAccountAdjustment,
            MaxAutoscaleServiceAccounts = MaxAutoscaleServiceAccounts
        };

        return orgUpdate;
    }
}
