using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Models.Business;
using Bit.Core.Models.StaticStore;

namespace Bit.Api.Models.Request.Organizations;

public class SecretsManagerSubscriptionUpdateRequestModel
{
    [Required]
    public int SeatAdjustment { get; set; }
    public int? MaxAutoscaleSeats { get; set; }
    public int ServiceAccountAdjustment { get; set; }
    public int? MaxAutoscaleServiceAccounts { get; set; }

    public virtual SecretsManagerSubscriptionUpdate ToSecretsManagerSubscriptionUpdate(Organization organization, Plan plan)
    {
        return new SecretsManagerSubscriptionUpdate(organization, plan, false)
        {
            MaxAutoscaleSmSeats = MaxAutoscaleSeats,
            MaxAutoscaleSmServiceAccounts = MaxAutoscaleServiceAccounts
        }
        .AdjustSeats(SeatAdjustment)
        .AdjustServiceAccounts(ServiceAccountAdjustment);
    }
}
