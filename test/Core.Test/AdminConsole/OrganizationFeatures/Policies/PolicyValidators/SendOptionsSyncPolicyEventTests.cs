#nullable enable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

[SutProviderCustomize]
public class SendOptionsSyncPolicyEventTests
{
    [Theory, BitAutoData]
    public async Task ExecutePostUpsertSideEffectAsync_ClearsDisableHideEmail_WhenPolicyDisabled(
        [PolicyUpdate(PolicyType.SendOptions, enabled: false)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SendOptions, enabled: false)] Policy postUpsertedPolicy,
        [Policy(PolicyType.SendControls, enabled: true)] Policy existingSendControlsPolicy,
        SutProvider<SendOptionsSyncPolicyEvent> sutProvider)
    {
        postUpsertedPolicy.OrganizationId = policyUpdate.OrganizationId;
        postUpsertedPolicy.SetDataModel(new SendOptionsPolicyData { DisableHideEmail = false });
        existingSendControlsPolicy.OrganizationId = policyUpdate.OrganizationId;
        existingSendControlsPolicy.SetDataModel(new SendControlsPolicyData { DisableSend = false, DisableHideEmail = true });

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.SendControls)
            .Returns(existingSendControlsPolicy);

        await sutProvider.Sut.ExecutePostUpsertSideEffectAsync(
            new SavePolicyModel(policyUpdate), postUpsertedPolicy, null);

        await sutProvider.GetDependency<IPolicyRepository>()
            .Received(1)
            .UpsertAsync(Arg.Is<Policy>(p =>
                p.Enabled == false &&
                (CoreHelpers.LoadClassFromJsonData<SendControlsPolicyData>(p.Data)!.DisableHideEmail == false)));
    }

    [Theory, BitAutoData]
    public async Task ExecutePostUpsertSideEffectAsync_UpdatesExistingSendControlsPolicy(
        [PolicyUpdate(PolicyType.SendOptions, enabled: true)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SendOptions, enabled: true)] Policy postUpsertedPolicy,
        [Policy(PolicyType.SendControls, enabled: false)] Policy existingSendControlsPolicy,
        SutProvider<SendOptionsSyncPolicyEvent> sutProvider)
    {
        postUpsertedPolicy.OrganizationId = policyUpdate.OrganizationId;
        postUpsertedPolicy.SetDataModel(new SendOptionsPolicyData { DisableHideEmail = true });
        existingSendControlsPolicy.OrganizationId = policyUpdate.OrganizationId;
        existingSendControlsPolicy.SetDataModel(new SendControlsPolicyData { DisableSend = false, DisableHideEmail = false });

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.SendControls)
            .Returns(existingSendControlsPolicy);

        await sutProvider.Sut.ExecutePostUpsertSideEffectAsync(
            new SavePolicyModel(policyUpdate), postUpsertedPolicy, null);

        await sutProvider.GetDependency<IPolicyRepository>()
            .Received(1)
            .UpsertAsync(Arg.Is<Policy>(p =>
                p.Id == existingSendControlsPolicy.Id &&
                p.Enabled == true &&
                (CoreHelpers.LoadClassFromJsonData<SendControlsPolicyData>(p.Data)!.DisableHideEmail == true)));
    }

    [Theory, BitAutoData]
    public async Task ExecutePostUpsertSideEffectAsync_CreatesNewSendControlsPolicy_WhenNoneExists(
        [PolicyUpdate(PolicyType.SendOptions, enabled: true)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SendOptions, enabled: true)] Policy postUpsertedPolicy,
        SutProvider<SendOptionsSyncPolicyEvent> sutProvider)
    {
        postUpsertedPolicy.OrganizationId = policyUpdate.OrganizationId;
        postUpsertedPolicy.SetDataModel(new SendOptionsPolicyData { DisableHideEmail = true });

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.SendControls)
            .Returns((Policy?)null);

        await sutProvider.Sut.ExecutePostUpsertSideEffectAsync(
            new SavePolicyModel(policyUpdate), postUpsertedPolicy, null);

        await sutProvider.GetDependency<IPolicyRepository>()
            .Received(1)
            .UpsertAsync(Arg.Is<Policy>(p =>
                p.OrganizationId == policyUpdate.OrganizationId &&
                p.Type == PolicyType.SendControls &&
                p.Enabled == true &&
                CoreHelpers.LoadClassFromJsonData<SendControlsPolicyData>(p.Data)!.DisableHideEmail == true));
    }
}
