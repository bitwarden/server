using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;

public class PolicyQuery(
    IPolicyRepository policyRepository,
    IEnumerable<IPolicyRequirementFactory<IPolicyRequirement>> factories) : IPolicyQuery
{
    public async Task<IEnumerable<PolicyStatus>> GetAllAsync(Guid organizationId)
    {
        var policies = await policyRepository.GetManyByOrganizationIdAsync(organizationId);
        var results = policies.Select(p => new PolicyStatus(organizationId, p.Type, p)).ToList();

        // Remove this block once migration from legacy Send policies > SendControls has run
        if (policies.All(p => p.Type != PolicyType.SendControls))
        {
            var synthesized = await SynthesizeSendControlsStatusAsync(organizationId);
            results.Add(synthesized);
        }

        // A policy that is enabled by default with no saved row is still enabled for the organization.
        var missingDefaultOnTypes = DefaultOnPolicyTypes()
            .Where(type => results.All(r => r.Type != type));
        results.AddRange(missingDefaultOnTypes
            .Select(type => new PolicyStatus(organizationId, type) { Enabled = true }));

        return results;
    }

    public async Task<PolicyStatus> RunAsync(Guid organizationId, PolicyType policyType)
    {
        var dbPolicy = await policyRepository.GetByOrganizationIdTypeAsync(organizationId, policyType);

        // Remove this block and SynthesizeSendControlsStatusAsync once migration has run
        if (dbPolicy == null && policyType == PolicyType.SendControls)
        {
            return await SynthesizeSendControlsStatusAsync(organizationId);
        }

        // A policy that is enabled by default with no saved row is enabled for the organization. An explicitly
        // disabled row still yields Enabled = false via the PolicyStatus constructor.
        if (dbPolicy == null && IsEnabledByDefault(policyType))
        {
            return new PolicyStatus(organizationId, policyType) { Enabled = true };
        }

        return new PolicyStatus(organizationId, policyType, dbPolicy);
    }

    private bool IsEnabledByDefault(PolicyType policyType)
        => factories.Any(f => f.PolicyType == policyType && f.DefaultState == PolicyDefaultState.Enabled);

    private IEnumerable<PolicyType> DefaultOnPolicyTypes()
        => factories.Where(f => f.DefaultState == PolicyDefaultState.Enabled).Select(f => f.PolicyType);

    /// <summary>
    /// When no SendControls policy row exists in the database, synthesizes a PolicyStatus
    /// from the legacy DisableSend and SendOptions policies. This supports lazy migration
    /// from the two legacy policies into the unified SendControls policy without requiring
    /// a database migration script.
    /// </summary>
    private async Task<PolicyStatus> SynthesizeSendControlsStatusAsync(Guid organizationId)
    {
        var disableSendPolicy = await policyRepository.GetByOrganizationIdTypeAsync(
            organizationId, PolicyType.DisableSend);
        var sendOptionsPolicy = await policyRepository.GetByOrganizationIdTypeAsync(
            organizationId, PolicyType.SendOptions);

        var disableSend = disableSendPolicy?.Enabled ?? false;
        var disableHideEmail = sendOptionsPolicy?.GetDataModel<SendOptionsPolicyData>().DisableHideEmail ?? false;
        var enabled = (disableSendPolicy?.Enabled ?? false) || (sendOptionsPolicy?.Enabled ?? false);

        var data = new SendControlsPolicyData
        {
            DisableSend = disableSend,
            DisableHideEmail = disableHideEmail,
        };

        return new PolicyStatus(organizationId, PolicyType.SendControls)
        {
            Enabled = enabled,
            Data = CoreHelpers.ClassToJsonData(data),
        };
    }
}
