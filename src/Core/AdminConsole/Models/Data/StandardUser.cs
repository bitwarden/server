using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.Models.Data;

public class StandardUser : IActingUser
{
    public StandardUser(Guid userId, bool isOrganizationOwner)
    {
        UserId = userId;
        IsOrganizationOwnerOrProvider = isOrganizationOwner;
    }

    public Guid? UserId { get; }
    public bool IsOrganizationOwnerOrProvider { get; }
    public EventSystemUser? SystemUserType => throw new Exception($"{nameof(StandardUser)} does not have a {nameof(SystemUserType)}");
}
