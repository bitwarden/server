using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.Models.Data;

public class ProviderUser(Guid userId, Guid providerId, ProviderUserType providerUserType) : IActingUser
{
    public Guid? UserId { get; } = userId;
    public Guid ProviderId { get; } = providerId;
    public ProviderUserType ProviderUserType { get; } = providerUserType;

    // Provider users aren't organization members but hold Owner-level authority over the organizations they manage.
    public bool IsOrganizationOwnerOrProvider => true;
    public static bool IsProvider => true;

    public EventSystemUser? SystemUserType =>
        throw new Exception($"{nameof(ProviderUser)} does not have a {nameof(SystemUserType)}");
}
