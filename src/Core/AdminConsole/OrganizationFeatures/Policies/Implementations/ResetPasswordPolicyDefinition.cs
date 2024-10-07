#nullable enable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;

public class ResetPasswordPolicyDefinition : IPolicyDefinition
{
    private readonly ISsoConfigRepository _ssoConfigRepository;
    public PolicyType Type => PolicyType.ResetPassword;
    public IEnumerable<PolicyType> RequiredPolicies => [PolicyType.SingleOrg];

    ResetPasswordPolicyDefinition(ISsoConfigRepository ssoConfigRepository)
    {
        _ssoConfigRepository = ssoConfigRepository;
    }

    public async Task<string?> ValidateAsync(Policy? currentPolicy, Policy modifiedPolicy)
    {
        if (modifiedPolicy is not { Enabled:true } ||
            modifiedPolicy.GetDataModel<ResetPasswordDataModel>()?.AutoEnrollEnabled == false)
        {
            var ssoConfig = await _ssoConfigRepository.GetByOrganizationIdAsync(modifiedPolicy.OrganizationId);
            if (ssoConfig?.GetData()?.MemberDecryptionType == MemberDecryptionType.TrustedDeviceEncryption)
            {
                return "Trusted device encryption is on and requires this policy.";
            }
        }

        return null;
    }
}
