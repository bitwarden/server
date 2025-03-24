using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.Models.StaticStore;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.PasswordManager;

public class PasswordManagerSubscriptionUpdate
{
    /// <summary>
    /// Seats the organization is currently paying for
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

    /// <summary>
    /// If the new seat total is equal to the organization's auto-scale seat count
    /// </summary>
    public bool MaxSeatsReached => UpdatedSeatTotal.HasValue && MaxAutoScaleSeats.HasValue && UpdatedSeatTotal.Value == MaxAutoScaleSeats.Value;

    public Plan.PasswordManagerPlanFeatures PasswordManagerPlan { get; }

    private PasswordManagerSubscriptionUpdate(int? organizationSeats,
        int? organizationAutoScaleSeatLimit,
        int currentSeats,
        int newUsersToAdd,
        Plan.PasswordManagerPlanFeatures plan)
    {
        Seats = organizationSeats;
        MaxAutoScaleSeats = organizationAutoScaleSeatLimit;
        OccupiedSeats = currentSeats;
        NewUsersToAdd = newUsersToAdd;
        PasswordManagerPlan = plan;
    }

    public PasswordManagerSubscriptionUpdate(InviteOrganization inviteOrganization, int occupiedSeats, int newUsersToAdd) :
        this(
            organizationSeats: inviteOrganization.Seats,
            organizationAutoScaleSeatLimit: inviteOrganization.MaxAutoScaleSeats,
            currentSeats: occupiedSeats,
            newUsersToAdd: newUsersToAdd,
            plan: inviteOrganization.Plan.PasswordManager)
    { }

    public PasswordManagerSubscriptionUpdate(InviteUserOrganizationValidationRequest validationRequest) :
        this(
            organizationSeats: validationRequest.InviteOrganization.Seats,
            organizationAutoScaleSeatLimit: validationRequest.InviteOrganization.MaxAutoScaleSeats,
            currentSeats: validationRequest.OccupiedPmSeats,
            newUsersToAdd: validationRequest.Invites.Length,
            plan: validationRequest.InviteOrganization.Plan.PasswordManager)
    { }
}
