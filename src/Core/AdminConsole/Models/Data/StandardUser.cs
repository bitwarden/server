using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.Models.Data;

public class StandardUser : IActingUser
{
    public StandardUser(Guid userId, bool isOrganizationOwner)
    {
        UserId = userId;
        IsOrganizationOwner = isOrganizationOwner;
    }

    public Guid? UserId { get; }
    public bool IsOrganizationOwner { get; }
    public EventSystemUser? SystemUserType => throw new Exception($"{nameof(StandardUser)} does not have a {nameof(SystemUserType)}");
}
