using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.Models.Business.Tokenables;

public interface IOrgUserInviteTokenableFactory
{
    OrgUserInviteTokenable CreateToken(OrganizationUser orgUser);
}
