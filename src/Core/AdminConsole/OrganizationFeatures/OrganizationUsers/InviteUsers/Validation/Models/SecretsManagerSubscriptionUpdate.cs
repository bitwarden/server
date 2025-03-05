using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.Models.StaticStore;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Models;

public class SecretsManagerSubscriptionUpdate
{
    public bool UseSecretsManger { get; }
    public int? Seats { get; }
    public int? MaxAutoScaleSeats { get; }
    public int OccupiedSeats { get; }
    public int AdditionalSeats { get; }
    public int? PasswordManagerUpdatedSeatTotal { get; }
    public Plan.SecretsManagerPlanFeatures SecretsManagerPlan { get; }
    public int? AvailableSeats => Seats - OccupiedSeats;
    public int SeatsRequiredToAdd => AdditionalSeats - AvailableSeats ?? 0;
    public int? UpdatedSeatTotal => Seats + SeatsRequiredToAdd;

    private SecretsManagerSubscriptionUpdate(bool useSecretsManger,
        int? organizationSeats,
        int? organizationAutoScaleSeatLimit,
        int currentSeats,
        int seatsToAdd,
        int? passwordManagerUpdatedSeatTotal,
        Plan.SecretsManagerPlanFeatures plan)
    {
        UseSecretsManger = useSecretsManger;
        Seats = organizationSeats;
        MaxAutoScaleSeats = organizationAutoScaleSeatLimit;
        OccupiedSeats = currentSeats;
        AdditionalSeats = seatsToAdd;
        PasswordManagerUpdatedSeatTotal = passwordManagerUpdatedSeatTotal;
        SecretsManagerPlan = plan;
    }

    public static SecretsManagerSubscriptionUpdate Create(InviteOrganization inviteOrganization, int occupiedSeats, int seatsToAdd, int passwordManagerSeatTotal)
    {
        return new SecretsManagerSubscriptionUpdate(inviteOrganization.UseSecretsManager,
            inviteOrganization.SmSeats,
            inviteOrganization.SmMaxAutoScaleSeats,
            occupiedSeats,
            seatsToAdd,
            passwordManagerSeatTotal,
            inviteOrganization.Plan.SecretsManager);
    }

    public static SecretsManagerSubscriptionUpdate Create(InviteUserOrganizationValidationRequest refined, PasswordManagerSubscriptionUpdate passwordManagerSubscriptionUpdate)
    {
        return new SecretsManagerSubscriptionUpdate(refined.InviteOrganization.UseSecretsManager,
            refined.InviteOrganization.SmSeats,
            refined.InviteOrganization.SmMaxAutoScaleSeats,
            refined.OccupiedSmSeats,
            refined.Invites.Count(x => x.AccessSecretsManager),
            passwordManagerSubscriptionUpdate.UpdatedSeatTotal,
            refined.InviteOrganization.Plan.SecretsManager);
    }
}
