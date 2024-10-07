#nullable enable

using Bit.Core.AdminConsole.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;

public class PasswordGeneratorPolicyDefinition : IPolicyDefinition
{
    public PolicyType Type => PolicyType.PasswordGenerator;
}
