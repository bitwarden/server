using Bit.Core.AdminConsole.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;

public class ActivateAutofillPolicyDefinition : IPolicyDefinition
{
    public PolicyType Type => PolicyType.ActivateAutofill;
}
