using Bit.Core.Enums;

namespace Bit.Identity.Utilities;

public interface ILoginApprovingClientTypes
{
    IReadOnlyCollection<ClientType> TypesThatCanApprove { get; }
}

public class LoginApprovingClientTypes : ILoginApprovingClientTypes
{
    public LoginApprovingClientTypes()
    {
        TypesThatCanApprove = new List<ClientType>
        {
            ClientType.Desktop,
            ClientType.Mobile,
            ClientType.Web,
            ClientType.Browser,
        };
    }

    public IReadOnlyCollection<ClientType> TypesThatCanApprove { get; }
}
