using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
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
        var newTotalSeats = organization.SmSeats.GetValueOrDefault() + SeatAdjustment;
        var newTotalServiceAccounts = organization.SmServiceAccounts.GetValueOrDefault() + ServiceAccountAdjustment;
        
        var orgUpdate = new SecretsManagerSubscriptionUpdate
        {
            OrganizationId = organization.Id,
            SeatAdjustment = SeatAdjustment,
            MaxAutoscaleSeats = MaxAutoscaleSeats,
            ServiceAccountsAdjustment = ServiceAccountAdjustment,
            MaxAutoscaleServiceAccounts = MaxAutoscaleServiceAccounts,
            
            NewTotalSeats = newTotalSeats,
            NewAdditionalSeats = newTotalSeats - plan.BaseSeats,
            
            NewTotalServiceAccounts = newTotalServiceAccounts,
            NewAdditionalServiceAccounts = newTotalServiceAccounts - plan.BaseServiceAccount.GetValueOrDefault(),
        };

        return orgUpdate;
    }
}
