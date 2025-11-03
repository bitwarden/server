using Bit.Core.Entities;

namespace Bit.Core.Auth.Models.Business.Tokenables;

public interface IOrgUserInviteTokenableFactory
{
    OrgUserInviteTokenable CreateToken(OrganizationUser orgUser);
}
