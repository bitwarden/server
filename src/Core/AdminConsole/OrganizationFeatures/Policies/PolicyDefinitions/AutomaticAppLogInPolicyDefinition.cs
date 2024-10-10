using Bit.Core.AdminConsole.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;

public class AutomaticAppLogInPolicyDefinition : IPolicyDefinition
{
    public PolicyType Type => PolicyType.AutomaticAppLogIn;
}
