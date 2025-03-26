using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.PasswordManager;
using Bit.Core.Models.StaticStore;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.SecretsManager;

public class SecretsManagerSubscriptionUpdate
{
    public bool UseSecretsManger { get; }

    /// <summary>
    /// Seats the organization has
    /// </summary>
    public int? Seats { get; }

    /// <summary>
    /// Max number of seats that the organization can have
    /// </summary>
    public int? MaxAutoScaleSeats { get; }

    /// <summary>
    /// Seats currently occupied by current users
    /// </summary>
    public int OccupiedSeats { get; }

    /// <summary>
    /// Users to add to the organization seats
    /// </summary>
    public int NewUsersToAdd { get; }

    public int? PasswordManagerUpdatedSeatTotal { get; }
    public Plan.SecretsManagerPlanFeatures SecretsManagerPlan { get; }

    /// <summary>
    /// Number of seats available for users
    /// </summary>
    public int? AvailableSeats => Seats - OccupiedSeats;

    /// <summary>
    /// Number of seats to scale the organization to.
    ///
    /// If Organization has no seat limit (Seats is null), then there are no new seats to add.
    /// </summary>
    public int SeatsRequiredToAdd => AvailableSeats.HasValue ? Math.Max(NewUsersToAdd - AvailableSeats.Value, 0) : 0;

    /// <summary>
    /// New total of seats for the organization
    /// </summary>
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
        NewUsersToAdd = seatsToAdd;
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
