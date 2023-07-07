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
        var autoscaleSeats = MaxAutoscaleSeats.HasValue && MaxAutoscaleSeats !=
                             organization.MaxAutoscaleSmSeats.GetValueOrDefault();
        var autoscaleServiceAccounts = MaxAutoscaleServiceAccounts.HasValue &&
                                       MaxAutoscaleServiceAccounts !=
                                       organization.MaxAutoscaleSmServiceAccounts.GetValueOrDefault();

        var orgUpdate = new SecretsManagerSubscriptionUpdate
        {
            OrganizationId = organization.Id,

            SmSeatsAdjustment = SeatAdjustment,
            SmSeats = newTotalSeats,
            SmSeatsExcludingBase = newTotalSeats - plan.BaseSeats,

            MaxAutoscaleSmSeats = MaxAutoscaleSeats,

            SmServiceAccountsAdjustment = ServiceAccountAdjustment,
            SmServiceAccounts = newTotalServiceAccounts,
            SmServiceAccountsExcludingBase = newTotalServiceAccounts - plan.BaseServiceAccount.GetValueOrDefault(),

            MaxAutoscaleSmServiceAccounts = MaxAutoscaleServiceAccounts,

            MaxAutoscaleSmSeatsChanged = autoscaleSeats,
            MaxAutoscaleSmServiceAccountsChanged = autoscaleServiceAccounts
        };

        return orgUpdate;
    }
}
