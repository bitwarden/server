namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
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

[SutProviderCustomize]
public class RequireSsoPolicyValidatorTests
{
    [Theory, BitAutoData]
    public async Task ValidateAsync_DisablingPolicy_KeyConnectorEnabled_ValidationError(
        [PolicyUpdate(PolicyType.SingleOrg, false)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SingleOrg)] Policy policy,
        SutProvider<RequireSsoPolicyValidator> sutProvider)
    {
        policy.OrganizationId = policyUpdate.OrganizationId;

        var ssoConfig = new SsoConfig { Enabled = true };
        ssoConfig.SetData(new SsoConfigurationData { MemberDecryptionType = MemberDecryptionType.KeyConnector });

        sutProvider.GetDependency<ISsoConfigRepository>()
            .GetByOrganizationIdAsync(policyUpdate.OrganizationId)
            .Returns(ssoConfig);

        var result = await sutProvider.Sut.ValidateAsync(policyUpdate, policy);
        Assert.Contains("Key Connector is enabled", result, StringComparison.OrdinalIgnoreCase);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_DisablingPolicy_TdeEnabled_ValidationError(
        [PolicyUpdate(PolicyType.SingleOrg, false)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SingleOrg)] Policy policy,
        SutProvider<RequireSsoPolicyValidator> sutProvider)
    {
        policy.OrganizationId = policyUpdate.OrganizationId;

        var ssoConfig = new SsoConfig { Enabled = true };
        ssoConfig.SetData(new SsoConfigurationData { MemberDecryptionType = MemberDecryptionType.TrustedDeviceEncryption });

        sutProvider.GetDependency<ISsoConfigRepository>()
            .GetByOrganizationIdAsync(policyUpdate.OrganizationId)
            .Returns(ssoConfig);

        var result = await sutProvider.Sut.ValidateAsync(policyUpdate, policy);
        Assert.Contains("Trusted device encryption is on", result, StringComparison.OrdinalIgnoreCase);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_DisablingPolicy_DecryptionOptionsNotEnabled_Success(
        [PolicyUpdate(PolicyType.ResetPassword, false)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.ResetPassword)] Policy policy,
        SutProvider<RequireSsoPolicyValidator> sutProvider)
    {
        policy.OrganizationId = policyUpdate.OrganizationId;

        var ssoConfig = new SsoConfig { Enabled = false };

        sutProvider.GetDependency<ISsoConfigRepository>()
            .GetByOrganizationIdAsync(policyUpdate.OrganizationId)
            .Returns(ssoConfig);

        var result = await sutProvider.Sut.ValidateAsync(policyUpdate, policy);
        Assert.True(string.IsNullOrEmpty(result));
    }
}
