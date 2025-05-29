using Bit.Core;
using Bit.Core.Enums;
using Bit.Core.Services;

namespace Bit.Identity.Utilities;

public interface ILoginApprovingClientTypes
{
    IReadOnlyCollection<ClientType> TypesThatCanApprove { get; }
}

public class LoginApprovingClientTypes : ILoginApprovingClientTypes
{
    public LoginApprovingClientTypes(
        IFeatureService featureService)
    {
        if (featureService.IsEnabled(FeatureFlagKeys.BrowserExtensionLoginApproval))
        {
            TypesThatCanApprove = new List<ClientType>
            {
                ClientType.Desktop,
                ClientType.Mobile,
                ClientType.Web,
                ClientType.Browser,
            };
        }
        else
        {
            TypesThatCanApprove = new List<ClientType>
            {
                ClientType.Desktop,
                ClientType.Mobile,
                ClientType.Web,
            };
        }
    }

    public IReadOnlyCollection<ClientType> TypesThatCanApprove { get; }
}
