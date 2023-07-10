using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Models.Business;

namespace Bit.Api.Models.Request.Organizations;

public class SecretsManagerSubscriptionUpdateRequestModel
{
    [Required]
    public int SeatAdjustment { get; set; }
    public int? MaxAutoscaleSeats { get; set; }
    public int ServiceAccountAdjustment { get; set; }
    public int? MaxAutoscaleServiceAccounts { get; set; }

    public virtual SecretsManagerSubscriptionUpdate ToSecretsManagerSubscriptionUpdate(Organization organization)
    {
        var orgUpdate = new SecretsManagerSubscriptionUpdate(
            organization,
            SeatAdjustment, MaxAutoscaleSeats,
            ServiceAccountAdjustment, MaxAutoscaleServiceAccounts);

        return orgUpdate;
    }
}
