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
public class DisableSendSyncPolicyValidatorTests
{
    [Theory, BitAutoData]
    public async Task ExecutePostUpsertSideEffectAsync_CreatesNewSendControlsPolicy_WhenNoneExists(
        [PolicyUpdate(PolicyType.DisableSend, enabled: true)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.DisableSend, enabled: true)] Policy postUpsertedPolicy,
        SutProvider<DisableSendSyncPolicyValidator> sutProvider)
    {
        postUpsertedPolicy.OrganizationId = policyUpdate.OrganizationId;
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
                (CoreHelpers.LoadClassFromJsonData<SendControlsPolicyData>(p.Data)!.DisableSend == true)));
    }

    [Theory, BitAutoData]
    public async Task ExecutePostUpsertSideEffectAsync_UpdatesExistingSendControlsPolicy(
        [PolicyUpdate(PolicyType.DisableSend, enabled: false)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.DisableSend, enabled: false)] Policy postUpsertedPolicy,
        [Policy(PolicyType.SendControls, enabled: true)] Policy existingSendControlsPolicy,
        SutProvider<DisableSendSyncPolicyValidator> sutProvider)
    {
        postUpsertedPolicy.OrganizationId = policyUpdate.OrganizationId;
        existingSendControlsPolicy.OrganizationId = policyUpdate.OrganizationId;
        existingSendControlsPolicy.SetDataModel(new SendControlsPolicyData { DisableSend = true, DisableHideEmail = false });

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.SendControls)
            .Returns(existingSendControlsPolicy);

        await sutProvider.Sut.ExecutePostUpsertSideEffectAsync(
            new SavePolicyModel(policyUpdate), postUpsertedPolicy, null);

        await sutProvider.GetDependency<IPolicyRepository>()
            .Received(1)
            .UpsertAsync(Arg.Is<Policy>(p =>
                p.Id == existingSendControlsPolicy.Id &&
                p.Enabled == false &&
                (CoreHelpers.LoadClassFromJsonData<SendControlsPolicyData>(p.Data)!.DisableSend == false)));
    }

    [Theory, BitAutoData]
    public async Task ExecutePostUpsertSideEffectAsync_SendControlsEnabled_WhenEitherFieldIsTrue(
        [PolicyUpdate(PolicyType.DisableSend, enabled: false)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.DisableSend, enabled: false)] Policy postUpsertedPolicy,
        [Policy(PolicyType.SendControls, enabled: true)] Policy existingSendControlsPolicy,
        SutProvider<DisableSendSyncPolicyValidator> sutProvider)
    {
        postUpsertedPolicy.OrganizationId = policyUpdate.OrganizationId;
        existingSendControlsPolicy.OrganizationId = policyUpdate.OrganizationId;
        // DisableSend is being turned off, but DisableHideEmail is still true
        existingSendControlsPolicy.SetDataModel(new SendControlsPolicyData { DisableSend = true, DisableHideEmail = true });

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.SendControls)
            .Returns(existingSendControlsPolicy);

        await sutProvider.Sut.ExecutePostUpsertSideEffectAsync(
            new SavePolicyModel(policyUpdate), postUpsertedPolicy, null);

        await sutProvider.GetDependency<IPolicyRepository>()
            .Received(1)
            .UpsertAsync(Arg.Is<Policy>(p =>
                p.Enabled == true)); // stays enabled because DisableHideEmail is still true
    }
}
