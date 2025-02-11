using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.Settings;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

public class SendPolicyRequirement : IPolicyRequirement
{
    public bool DisableSend { get; init; }
    public bool DisableHideEmail { get; init; }

    public static SendPolicyRequirement Create(IEnumerable<PolicyDetails> userPolicyDetails)
    {
        var filteredPolicies = userPolicyDetails
            .ExcludeOwnersAndAdmins()
            .ExcludeRevokedAndInvitedUsers()
            .ToList();

        return new SendPolicyRequirement
        {
            DisableSend = filteredPolicies
                .GetPolicyType(PolicyType.DisableSend)
                .Any(),

            DisableHideEmail = filteredPolicies
                .GetPolicyType(PolicyType.SendOptions)
                .Select(up => up.GetDataModel<SendOptionsPolicyData>())
                .Any(d => d.DisableHideEmail)
        };
    }
}

public class TestPolicyRequirement : IPolicyRequirement
{
    public Guid OrganizationId { get; init; }
    public Guid InstallationId { get; init; }
    public static TestPolicyRequirement Create(IGlobalSettings globalSettings, IEnumerable<PolicyDetails> policyDetails)
        => new() { OrganizationId = policyDetails.First().OrganizationId, InstallationId = globalSettings.Installation.Id };
}
