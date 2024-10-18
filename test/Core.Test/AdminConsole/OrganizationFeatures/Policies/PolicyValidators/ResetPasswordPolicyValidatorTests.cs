using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

[SutProviderCustomize]
public class ResetPasswordPolicyValidatorTests
{
    [Theory]
    [BitAutoData(true, false)]
    [BitAutoData(false, true)]
    [BitAutoData(false, false)]
    public async Task ValidateAsync_DisablingPolicy_TdeEnabled_ValidationError(
        bool policyEnabled,
        bool autoEnrollEnabled,
        [PolicyUpdate(PolicyType.ResetPassword)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.ResetPassword)] Policy policy,
        SutProvider<ResetPasswordPolicyValidator> sutProvider)
    {
        policyUpdate.Enabled = policyEnabled;
        policyUpdate.SetDataModel(new ResetPasswordDataModel
        {
            AutoEnrollEnabled = autoEnrollEnabled
        });
        policy.OrganizationId = policyUpdate.OrganizationId;

        var ssoConfig = new SsoConfig { Enabled = true };
        ssoConfig.SetData(new SsoConfigurationData { MemberDecryptionType = MemberDecryptionType.TrustedDeviceEncryption });

        sutProvider.GetDependency<ISsoConfigRepository>()
            .GetByOrganizationIdAsync(policyUpdate.OrganizationId)
            .Returns(ssoConfig);

        var result = await sutProvider.Sut.ValidateAsync(policyUpdate, policy);
        Assert.Contains("Trusted device encryption is on and requires this policy.", result, StringComparison.OrdinalIgnoreCase);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_DisablingPolicy_TdeNotEnabled_Success(
        [PolicyUpdate(PolicyType.ResetPassword, false)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.ResetPassword)] Policy policy,
        SutProvider<ResetPasswordPolicyValidator> sutProvider)
    {
        policyUpdate.SetDataModel(new ResetPasswordDataModel
        {
            AutoEnrollEnabled = false
        });
        policy.OrganizationId = policyUpdate.OrganizationId;

        var ssoConfig = new SsoConfig { Enabled = false };

        sutProvider.GetDependency<ISsoConfigRepository>()
            .GetByOrganizationIdAsync(policyUpdate.OrganizationId)
            .Returns(ssoConfig);

        var result = await sutProvider.Sut.ValidateAsync(policyUpdate, policy);
        Assert.True(string.IsNullOrEmpty(result));
    }
}
