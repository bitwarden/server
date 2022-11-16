using Bit.Core.Enums;

namespace Bit.Core.Models;

public abstract class OrganizationSeatOccupant
{
    public OrganizationUserStatusType Status { get; set; }

    public bool OccupiesOrganizationSeat
    {
        get
        {
            return Status != OrganizationUserStatusType.Revoked;
        }
    }
}
