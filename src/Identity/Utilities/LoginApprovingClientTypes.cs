using Bit.Core.Enums;

namespace Bit.Identity.Utilities;

public static class LoginApprovingClientTypes
{
    private static readonly IReadOnlyCollection<ClientType> _clientTypesThatCanApprove;

    static LoginApprovingClientTypes()
    {
        var clientTypes = new List<ClientType>
        {
            ClientType.Desktop,
            ClientType.Mobile,
            ClientType.Web,
            ClientType.Browser,
        };
        _clientTypesThatCanApprove = clientTypes.AsReadOnly();
    }

    public static IReadOnlyCollection<ClientType> TypesThatCanApprove => _clientTypesThatCanApprove;
}
