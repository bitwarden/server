using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.PasswordManager;
using Bit.Core.Models.StaticStore;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.SecretsManager;

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

    public SecretsManagerSubscriptionUpdate(InviteOrganization inviteOrganization, int occupiedSeats, int seatsToAdd, int passwordManagerSeatTotal) :
        this(
            useSecretsManger: inviteOrganization.UseSecretsManager,
            organizationSeats: inviteOrganization.SmSeats,
            organizationAutoScaleSeatLimit: inviteOrganization.SmMaxAutoScaleSeats,
            currentSeats: occupiedSeats,
            seatsToAdd: seatsToAdd,
            passwordManagerUpdatedSeatTotal: passwordManagerSeatTotal,
            plan: inviteOrganization.Plan.SecretsManager)
    { }

    public SecretsManagerSubscriptionUpdate(InviteUserOrganizationValidationRequest request,
        PasswordManagerSubscriptionUpdate passwordManagerSubscriptionUpdate) :
        this(
            useSecretsManger: request.InviteOrganization.UseSecretsManager,
            organizationSeats: request.InviteOrganization.SmSeats,
            organizationAutoScaleSeatLimit: request.InviteOrganization.SmMaxAutoScaleSeats,
            currentSeats: request.OccupiedSmSeats,
            seatsToAdd: request.Invites.Count(x => x.AccessSecretsManager),
            passwordManagerUpdatedSeatTotal: passwordManagerSubscriptionUpdate.UpdatedSeatTotal,
            plan: request.InviteOrganization.Plan.SecretsManager)
    { }
}
