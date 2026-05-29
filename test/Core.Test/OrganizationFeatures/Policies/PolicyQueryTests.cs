using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.Policies;

[SutProviderCustomize]
public class PolicyQueryTests
{
    [Theory, BitAutoData]
    public async Task RunAsync_WithExistingPolicy_ReturnsPolicy(SutProvider<PolicyQuery> sutProvider,
        Policy policy)
    {
        // Arrange
        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policy.OrganizationId, policy.Type)
            .Returns(policy);

        // Act
        var policyData = await sutProvider.Sut.RunAsync(policy.OrganizationId, policy.Type);

        // Assert
        Assert.Equal(policy.Data, policyData.Data);
        Assert.Equal(policy.Type, policyData.Type);
        Assert.Equal(policy.Enabled, policyData.Enabled);
        Assert.Equal(policy.OrganizationId, policyData.OrganizationId);
    }

    [Theory, BitAutoData]
    public async Task RunAsync_WithNonExistentPolicy_ReturnsDefaultDisabledPolicy(
        SutProvider<PolicyQuery> sutProvider,
        Guid organizationId,
        PolicyType policyType)
    {
        // Arrange
        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(organizationId, policyType)
            .ReturnsNull();

        // Act
        var policyData = await sutProvider.Sut.RunAsync(organizationId, policyType);

        // Assert
        Assert.Equal(organizationId, policyData.OrganizationId);
        Assert.Equal(policyType, policyData.Type);
        Assert.False(policyData.Enabled);
        Assert.Null(policyData.Data);
    }

    [Theory, BitAutoData]
    public async Task GetAllAsync_WithSendControlsInDb_ReturnsAsIs(
        SutProvider<PolicyQuery> sutProvider,
        Guid organizationId)
    {
        // Arrange
        var sendControlsPolicy = new Policy
        {
            OrganizationId = organizationId,
            Type = PolicyType.SendControls,
            Enabled = true,
            Data = CoreHelpers.ClassToJsonData(new SendControlsPolicyData
            {
                DisableSend = true,
                DisableHideEmail = false,
            }),
        };
        var otherPolicy = new Policy
        {
            OrganizationId = organizationId,
            Type = PolicyType.SingleOrg,
            Enabled = true,
        };

        sutProvider.GetDependency<IPolicyRepository>()
            .GetManyByOrganizationIdAsync(organizationId)
            .Returns(new List<Policy> { sendControlsPolicy, otherPolicy });

        // Act
        var results = (await sutProvider.Sut.GetAllAsync(organizationId)).ToList();

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Contains(results, p => p.Type == PolicyType.SendControls);
        Assert.Contains(results, p => p.Type == PolicyType.SingleOrg);

        // Should not attempt to synthesize
        // Aggregation is not necessary if DB is already reporting SendControls policy state
        await sutProvider.GetDependency<IPolicyRepository>()
            .DidNotReceive()
            .GetByOrganizationIdTypeAsync(organizationId, PolicyType.DisableSend);
    }

    [Theory, BitAutoData]
    public async Task GetAllAsync_WithoutSendControls_LegacyEnabled_SynthesizesSendControls(
        SutProvider<PolicyQuery> sutProvider,
        Guid organizationId)
    {
        // Arrange — legacy DisableSend is enabled, no SendControls row
        var disableSendPolicy = new Policy
        {
            OrganizationId = organizationId,
            Type = PolicyType.DisableSend,
            Enabled = true,
        };
        var singleOrgPolicy = new Policy
        {
            OrganizationId = organizationId,
            Type = PolicyType.SingleOrg,
            Enabled = true,
        };

        sutProvider.GetDependency<IPolicyRepository>()
            .GetManyByOrganizationIdAsync(organizationId)
            .Returns(new List<Policy> { disableSendPolicy, singleOrgPolicy });

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(organizationId, PolicyType.DisableSend)
            .Returns(disableSendPolicy);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(organizationId, PolicyType.SendOptions)
            .ReturnsNull();

        // Act
        var results = (await sutProvider.Sut.GetAllAsync(organizationId)).ToList();

        // Assert — original 2 + synthesized SendControls
        Assert.Equal(3, results.Count);
        var synthesized = results.Single(p => p.Type == PolicyType.SendControls);
        Assert.True(synthesized.Enabled);
        var data = synthesized.GetDataModel<SendControlsPolicyData>();
        Assert.True(data.DisableSend);
        Assert.False(data.DisableHideEmail);
    }

    [Theory, BitAutoData]
    public async Task GetAllAsync_WithoutSendControls_NoLegacyEnabled_SynthesizesDisabledEntry(
        SutProvider<PolicyQuery> sutProvider,
        Guid organizationId)
    {
        // Arrange — no SendControls, legacy policies disabled
        var singleOrgPolicy = new Policy
        {
            OrganizationId = organizationId,
            Type = PolicyType.SingleOrg,
            Enabled = true,
        };

        sutProvider.GetDependency<IPolicyRepository>()
            .GetManyByOrganizationIdAsync(organizationId)
            .Returns(new List<Policy> { singleOrgPolicy });

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(organizationId, PolicyType.DisableSend)
            .ReturnsNull();
        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(organizationId, PolicyType.SendOptions)
            .ReturnsNull();

        // Act
        var results = (await sutProvider.Sut.GetAllAsync(organizationId)).ToList();

        // Assert — original policy + disabled synthesized SendControls entry
        Assert.Equal(2, results.Count);
        Assert.Contains(results, p => p.Type == PolicyType.SingleOrg);
        var synthesized = results.Single(p => p.Type == PolicyType.SendControls);
        Assert.False(synthesized.Enabled);
    }

    [Theory, BitAutoData]
    public async Task GetAllAsync_WithoutSendControls_DisabledSendOptionsWithConfig_PreservesDisableHideEmail(
        SutProvider<PolicyQuery> sutProvider,
        Guid organizationId)
    {
        // Arrange — SendOptions is disabled but has DisableHideEmail = true configured
        var sendOptionsPolicy = new Policy
        {
            OrganizationId = organizationId,
            Type = PolicyType.SendOptions,
            Enabled = false,
        };
        sendOptionsPolicy.SetDataModel(new SendOptionsPolicyData { DisableHideEmail = true });

        sutProvider.GetDependency<IPolicyRepository>()
            .GetManyByOrganizationIdAsync(organizationId)
            .Returns(new List<Policy> { sendOptionsPolicy });

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(organizationId, PolicyType.DisableSend)
            .ReturnsNull();
        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(organizationId, PolicyType.SendOptions)
            .Returns(sendOptionsPolicy);

        // Act
        var results = (await sutProvider.Sut.GetAllAsync(organizationId)).ToList();

        // Assert — synthesized SendControls preserves DisableHideEmail but is not enabled
        var synthesized = results.Single(p => p.Type == PolicyType.SendControls);
        Assert.False(synthesized.Enabled);
        var data = synthesized.GetDataModel<SendControlsPolicyData>();
        Assert.True(data.DisableHideEmail);
    }

    [Theory, BitAutoData]
    public async Task GetAllAsync_WithoutSendControls_EnabledSendOptionsWithDisableHideEmail_SynthesizesEnabledEntry(
        SutProvider<PolicyQuery> sutProvider,
        Guid organizationId)
    {
        // Arrange — SendOptions is enabled AND DisableHideEmail is true
        var sendOptionsPolicy = new Policy
        {
            OrganizationId = organizationId,
            Type = PolicyType.SendOptions,
            Enabled = true,
        };
        sendOptionsPolicy.SetDataModel(new SendOptionsPolicyData { DisableHideEmail = true });

        sutProvider.GetDependency<IPolicyRepository>()
            .GetManyByOrganizationIdAsync(organizationId)
            .Returns(new List<Policy> { sendOptionsPolicy });

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(organizationId, PolicyType.DisableSend)
            .ReturnsNull();
        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(organizationId, PolicyType.SendOptions)
            .Returns(sendOptionsPolicy);

        // Act
        var results = (await sutProvider.Sut.GetAllAsync(organizationId)).ToList();

        // Assert — Enabled derives from SendOptions.Enabled, DisableHideEmail from SendOptions data
        var synthesized = results.Single(p => p.Type == PolicyType.SendControls);
        Assert.True(synthesized.Enabled);
        var data = synthesized.GetDataModel<SendControlsPolicyData>();
        Assert.True(data.DisableHideEmail);
    }

    [Theory, BitAutoData]
    public async Task GetAllAsync_WithoutSendControls_EnabledSendOptionsWithoutDisableHideEmail_StillEnabled(
        SutProvider<PolicyQuery> sutProvider,
        Guid organizationId)
    {
        // Arrange — SendOptions is enabled but DisableHideEmail is false
        var sendOptionsPolicy = new Policy
        {
            OrganizationId = organizationId,
            Type = PolicyType.SendOptions,
            Enabled = true,
        };
        sendOptionsPolicy.SetDataModel(new SendOptionsPolicyData { DisableHideEmail = false });

        sutProvider.GetDependency<IPolicyRepository>()
            .GetManyByOrganizationIdAsync(organizationId)
            .Returns(new List<Policy> { sendOptionsPolicy });

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(organizationId, PolicyType.DisableSend)
            .ReturnsNull();
        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(organizationId, PolicyType.SendOptions)
            .Returns(sendOptionsPolicy);

        // Act
        var results = (await sutProvider.Sut.GetAllAsync(organizationId)).ToList();

        // Assert — Enabled is true because SendOptions is enabled, regardless of DisableHideEmail
        var synthesized = results.Single(p => p.Type == PolicyType.SendControls);
        Assert.True(synthesized.Enabled);
        var data = synthesized.GetDataModel<SendControlsPolicyData>();
        Assert.False(data.DisableHideEmail);
    }
}
