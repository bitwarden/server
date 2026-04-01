using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyEventHandlers;

public class ResetPasswordPolicyEventHandler : IPolicyValidationEvent, IEnforceDependentPoliciesEvent
{
    private readonly ISsoConfigRepository _ssoConfigRepository;
    public PolicyType Type => PolicyType.ResetPassword;
    public IEnumerable<PolicyType> RequiredPolicies => [PolicyType.SingleOrg];

    public ResetPasswordPolicyEventHandler(ISsoConfigRepository ssoConfigRepository)
    {
        _ssoConfigRepository = ssoConfigRepository;
    }

    public async Task<string> ValidateAsync(SavePolicyModel policyRequest, Policy? currentPolicy)
    {
        var policyUpdate = policyRequest.PolicyUpdate;

        if (policyUpdate is not { Enabled: true } ||
            policyUpdate.GetDataModel<ResetPasswordDataModel>().AutoEnrollEnabled == false)
        {
            var ssoConfig = await _ssoConfigRepository.GetByOrganizationIdAsync(policyUpdate.OrganizationId);
            return ssoConfig.ValidateDecryptionOptionsNotEnabled([MemberDecryptionType.TrustedDeviceEncryption]);
        }

        return "";
    }
}
