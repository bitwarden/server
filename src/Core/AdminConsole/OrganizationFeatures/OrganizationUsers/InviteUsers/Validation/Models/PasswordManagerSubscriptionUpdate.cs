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

    public Plan Plan { get; }

    private PasswordManagerSubscriptionUpdate(int? organizationSeats, int? organizationAutoScaleSeatLimit, int currentSeats, int seatsToAdd, Plan plan)
    {
        Seats = organizationSeats;
        MaxAutoScaleSeats = organizationAutoScaleSeatLimit;
        OccupiedSeats = currentSeats;
        AdditionalSeats = seatsToAdd;
        Plan = plan;
    }

    public static PasswordManagerSubscriptionUpdate Create(OrganizationDto organizationDto, int occupiedSeats, int seatsToAdd)
    {
        return new PasswordManagerSubscriptionUpdate(organizationDto.Seats, organizationDto.MaxAutoScaleSeats, occupiedSeats, seatsToAdd, organizationDto.Plan);
    }

    public static PasswordManagerSubscriptionUpdate Create(InviteUserOrganizationValidationRequest refined)
    {
        return new PasswordManagerSubscriptionUpdate(refined.Organization.Seats, refined.Organization.MaxAutoScaleSeats,
            refined.OccupiedPmSeats, refined.Invites.Length, refined.Organization.Plan);
    }
}
