using Bit.Core.AdminConsole.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;

public class DisablePersonalVaultExportPolicyDefinition : IPolicyDefinition
{
    public PolicyType Type => PolicyType.DisablePersonalVaultExport;
}
