#nullable enable

using Bit.Core.AdminConsole.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;

public class SendOptionsPolicyDefinition : IPolicyDefinition
{
    public PolicyType Type => PolicyType.SendOptions;
}
