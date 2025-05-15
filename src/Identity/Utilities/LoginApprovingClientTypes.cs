using Bit.Core.Enums;

namespace Bit.Identity.Utilities;

public static class LoginApprovingClientTypes
{
    private static readonly IReadOnlyCollection<ClientType> _clientTypesThatCanApprove;
    private static readonly IReadOnlyCollection<ClientType> _featureFlaggedClientTypesThatCanApprove;

    static LoginApprovingClientTypes()
    {
        var featureFlaggedClientTypes = new List<ClientType>
        {
            ClientType.Desktop,
            ClientType.Mobile,
            ClientType.Web,
            ClientType.Browser,
        };
        _featureFlaggedClientTypesThatCanApprove = featureFlaggedClientTypes.AsReadOnly();

        var clientTypes = new List<ClientType>
        {
            ClientType.Desktop,
            ClientType.Mobile,
            ClientType.Web,
        };
        _clientTypesThatCanApprove = clientTypes.AsReadOnly();
    }

    public static IReadOnlyCollection<ClientType> FeatureFlaggedTypesThatCanApprove => _featureFlaggedClientTypesThatCanApprove;
    public static IReadOnlyCollection<ClientType> TypesThatCanApprove => _clientTypesThatCanApprove;
}
