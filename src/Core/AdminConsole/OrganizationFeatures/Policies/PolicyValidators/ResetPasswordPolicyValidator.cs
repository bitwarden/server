#nullable enable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
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

    public async Task<string> ValidateAsync(Policy? currentPolicy, Policy modifiedPolicy)
    {
        if (modifiedPolicy is not { Enabled: true } ||
            modifiedPolicy.GetDataModel<ResetPasswordDataModel>().AutoEnrollEnabled == false)
        {
            return await ValidateDisableAsync(modifiedPolicy);
        }

        return "";
    }

    private async Task<string> ValidateDisableAsync(Policy policy)
    {
        var ssoConfig = await _ssoConfigRepository.GetByOrganizationIdAsync(policy.OrganizationId);
        return ssoConfig?.GetData().MemberDecryptionType switch
        {
            MemberDecryptionType.TrustedDeviceEncryption => "Trusted device encryption is on and requires this policy.",
            _ => ""
        };
    }
}
