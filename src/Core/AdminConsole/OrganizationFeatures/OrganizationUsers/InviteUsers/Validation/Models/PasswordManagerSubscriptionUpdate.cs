using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.Models.StaticStore;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Models;

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

    public static PasswordManagerSubscriptionUpdate Create(InviteOrganization inviteOrganization, int occupiedSeats, int seatsToAdd)
    {
        return new PasswordManagerSubscriptionUpdate(
            inviteOrganization.Seats,
            inviteOrganization.MaxAutoScaleSeats,
            occupiedSeats,
            seatsToAdd,
            inviteOrganization.Plan.PasswordManager);
    }

    public static PasswordManagerSubscriptionUpdate Create(InviteUserOrganizationValidationRequest validationRequest)
    {
        return new PasswordManagerSubscriptionUpdate(
            validationRequest.InviteOrganization.Seats,
            validationRequest.InviteOrganization.MaxAutoScaleSeats,
            validationRequest.OccupiedPmSeats,
            validationRequest.Invites.Length,
            validationRequest.InviteOrganization.Plan.PasswordManager);
    }
}
