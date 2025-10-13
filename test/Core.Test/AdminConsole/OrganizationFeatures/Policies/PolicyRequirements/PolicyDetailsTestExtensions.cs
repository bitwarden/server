using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.Utilities;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

public static class PolicyDetailsTestExtensions
{
    public static void SetDataModel<T>(this PolicyDetails policyDetails, T data) where T : IPolicyDataModel
        => policyDetails.PolicyData = CoreHelpers.ClassToJsonData(data);
}
