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
        return new SecretsManagerSubscriptionUpdate(organization, false)
        {
            MaxAutoscaleSmSeats = MaxAutoscaleSeats,
            MaxAutoscaleSmServiceAccounts = MaxAutoscaleServiceAccounts
        }
        .AdjustSeats(SeatAdjustment)
        .AdjustServiceAccounts(ServiceAccountAdjustment);
    }
}
