using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.Models.StaticStore;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.PasswordManager;

public class PasswordManagerSubscriptionUpdate
{
    /// <summary>
    /// Seats the organization has
    /// </summary>
    public int? Seats { get; }

    public int? MaxAutoScaleSeats { get; }

    public int OccupiedSeats { get; }

    public int AdditionalSeats { get; }

    public int? AvailableSeats => Seats - OccupiedSeats;

    public int SeatsRequiredToAdd => AdditionalSeats - AvailableSeats ?? 0;

    public int? UpdatedSeatTotal => Seats + SeatsRequiredToAdd;

    public bool MaxSeatsReached => UpdatedSeatTotal.HasValue && MaxAutoScaleSeats.HasValue && UpdatedSeatTotal.Value == MaxAutoScaleSeats.Value;

    public Plan.PasswordManagerPlanFeatures PasswordManagerPlan { get; }

    private PasswordManagerSubscriptionUpdate(int? organizationSeats,
        int? organizationAutoScaleSeatLimit,
        int currentSeats,
        int seatsToAdd,
        Plan.PasswordManagerPlanFeatures plan)
    {
        Seats = organizationSeats;
        MaxAutoScaleSeats = organizationAutoScaleSeatLimit;
        OccupiedSeats = currentSeats;
        AdditionalSeats = seatsToAdd;
        PasswordManagerPlan = plan;
    }

    public PasswordManagerSubscriptionUpdate(InviteOrganization inviteOrganization, int occupiedSeats, int seatsToAdd) :
        this(
            organizationSeats: inviteOrganization.Seats,
            organizationAutoScaleSeatLimit: inviteOrganization.MaxAutoScaleSeats,
            currentSeats: occupiedSeats,
            seatsToAdd: seatsToAdd,
            plan: inviteOrganization.Plan.PasswordManager)
    { }

    public PasswordManagerSubscriptionUpdate(InviteUserOrganizationValidationRequest validationRequest) :
        this(
            organizationSeats: validationRequest.InviteOrganization.Seats,
            organizationAutoScaleSeatLimit: validationRequest.InviteOrganization.MaxAutoScaleSeats,
            currentSeats: validationRequest.OccupiedPmSeats,
            seatsToAdd: validationRequest.Invites.Length,
            plan: validationRequest.InviteOrganization.Plan.PasswordManager)
    { }
}
