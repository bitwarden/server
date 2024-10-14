#nullable enable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

public class ResetPasswordPolicyValidator : IPolicyValidator
{
    private readonly ISsoConfigRepository _ssoConfigRepository;
    public PolicyType Type => PolicyType.ResetPassword;
    public IEnumerable<PolicyType> RequiredPolicies => [PolicyType.SingleOrg];

    public ResetPasswordPolicyValidator(ISsoConfigRepository ssoConfigRepository)
    {
        _ssoConfigRepository = ssoConfigRepository;
    }

    public async Task<string> ValidateAsync(PolicyUpdate policyUpdate, Policy? currentPolicy)
    {
        if (policyUpdate is not { Enabled: true } ||
            policyUpdate.GetDataModel<ResetPasswordDataModel>().AutoEnrollEnabled == false)
        {
            return await ValidateDisableAsync(policyUpdate);
        }

        return "";
    }

    private async Task<string> ValidateDisableAsync(PolicyUpdate policyUpdate)
    {
        var ssoConfig = await _ssoConfigRepository.GetByOrganizationIdAsync(policyUpdate.OrganizationId);
        return ssoConfig?.GetData().MemberDecryptionType switch
        {
            MemberDecryptionType.TrustedDeviceEncryption => "Trusted device encryption is on and requires this policy.",
            _ => ""
        };
    }
}
