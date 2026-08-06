using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.Utilities;

public static class OrganizationSeatAvailability
{
    public static bool HasAvailableSeats(Organization organization, int occupiedSeats) =>
        !organization.Seats.HasValue ||
        occupiedSeats < organization.Seats.Value ||
        !organization.MaxAutoscaleSeats.HasValue ||
        occupiedSeats < organization.MaxAutoscaleSeats.Value;
}
