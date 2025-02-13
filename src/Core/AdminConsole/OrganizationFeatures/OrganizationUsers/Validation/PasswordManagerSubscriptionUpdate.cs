using Bit.Core.AdminConsole.Models.Business;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Validation;

public record PasswordManagerSubscriptionUpdate
{
    /// <summary>
    /// Seats the organization has
    /// </summary>
    public int? Seats { get; private init; }

    public int? MaxAutoScaleSeats { get; private init; }

    public int OccupiedSeats { get; private init; }

    public int AdditionalSeats { get; private init; }

    public int? AvailableSeats => Seats - OccupiedSeats;

    public int SeatsRequiredToAdd => AdditionalSeats - AvailableSeats ?? 0;

    public int? UpdatedSeatTotal => Seats + SeatsRequiredToAdd;

    private PasswordManagerSubscriptionUpdate(int? organizationSeats, int? organizationAutoScaleSeatLimit, int currentSeats, int seatsToAdd)
    {
        Seats = organizationSeats;
        MaxAutoScaleSeats = organizationAutoScaleSeatLimit;
        OccupiedSeats = currentSeats;
        AdditionalSeats = seatsToAdd;
    }

    public static PasswordManagerSubscriptionUpdate Create(OrganizationDto organizationDto, int occupiedSeats, int seatsToAdd)
    {
        return new PasswordManagerSubscriptionUpdate(organizationDto.Seats, organizationDto.MaxAutoScaleSeats, occupiedSeats, seatsToAdd);
    }

    public static PasswordManagerSubscriptionUpdate Create(InviteOrganizationUserRefined refined)
    {
        return new PasswordManagerSubscriptionUpdate(refined.Organization.Seats, refined.Organization.MaxAutoScaleSeats,
            refined.OccupiedPmSeats, refined.Invites.Length);
    }
}
