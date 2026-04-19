using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Repositories;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyEventHandlers;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies.PolicyEventHandlers;

[SutProviderCustomize]
public class SendControlsSyncPolicyEventTests
{
    [Theory, BitAutoData]
    public async Task ExecutePostUpsertSideEffectAsync_SyncsDisableSend_ToLegacyDisableSendPolicy(
        [PolicyUpdate(PolicyType.SendControls, enabled: true)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SendControls, enabled: true)] Policy postUpsertedPolicy,
        SutProvider<SendControlsSyncPolicyEvent> sutProvider)
    {
        postUpsertedPolicy.OrganizationId = policyUpdate.OrganizationId;
        postUpsertedPolicy.SetDataModel(new SendControlsPolicyData { DisableSend = true, DisableHideEmail = false });

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.DisableSend)
            .Returns((Policy?)null);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.SendOptions)
            .Returns((Policy?)null);

        await sutProvider.Sut.ExecutePostUpsertSideEffectAsync(
            new SavePolicyModel(policyUpdate), postUpsertedPolicy, null);

        await sutProvider.GetDependency<IPolicyRepository>()
            .Received(1)
            .UpsertAsync(Arg.Is<Policy>(p =>
                p.OrganizationId == policyUpdate.OrganizationId &&
                p.Type == PolicyType.DisableSend &&
                p.Enabled == true));
    }

    [Theory, BitAutoData]
    public async Task ExecutePostUpsertSideEffectAsync_SyncsDisableHideEmail_ToLegacySendOptionsPolicy(
        [PolicyUpdate(PolicyType.SendControls, enabled: true)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SendControls, enabled: true)] Policy postUpsertedPolicy,
        SutProvider<SendControlsSyncPolicyEvent> sutProvider)
    {
        postUpsertedPolicy.OrganizationId = policyUpdate.OrganizationId;
        postUpsertedPolicy.SetDataModel(new SendControlsPolicyData { DisableSend = false, DisableHideEmail = true });

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.DisableSend)
            .Returns((Policy?)null);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.SendOptions)
            .Returns((Policy?)null);

        await sutProvider.Sut.ExecutePostUpsertSideEffectAsync(
            new SavePolicyModel(policyUpdate), postUpsertedPolicy, null);

        await sutProvider.GetDependency<IPolicyRepository>()
            .Received(1)
            .UpsertAsync(Arg.Is<Policy>(p =>
                p.OrganizationId == policyUpdate.OrganizationId &&
                p.Type == PolicyType.SendOptions &&
                p.Enabled == true &&
                (p.GetDataModel<SendOptionsPolicyData>().DisableHideEmail == true)));
    }

    [Theory, BitAutoData]
    public async Task ExecutePostUpsertSideEffectAsync_DisablesLegacyPolicies_WhenSendControlsPolicyDisabled(
        [PolicyUpdate(PolicyType.SendControls, enabled: false)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SendControls, enabled: false)] Policy postUpsertedPolicy,
        SutProvider<SendControlsSyncPolicyEvent> sutProvider)
    {
        postUpsertedPolicy.OrganizationId = policyUpdate.OrganizationId;
        postUpsertedPolicy.SetDataModel(new SendControlsPolicyData { DisableSend = true, DisableHideEmail = true });

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.DisableSend)
            .Returns((Policy?)null);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.SendOptions)
            .Returns((Policy?)null);

        await sutProvider.Sut.ExecutePostUpsertSideEffectAsync(
            new SavePolicyModel(policyUpdate), postUpsertedPolicy, null);

        await sutProvider.GetDependency<IPolicyRepository>()
            .Received(1)
            .UpsertAsync(Arg.Is<Policy>(p =>
                p.Type == PolicyType.DisableSend &&
                p.Enabled == false));
        await sutProvider.GetDependency<IPolicyRepository>()
            .Received(1)
            .UpsertAsync(Arg.Is<Policy>(p =>
                p.Type == PolicyType.SendOptions &&
                p.Enabled == false));
    }

    [Theory, BitAutoData]
    public async Task ExecutePostUpsertSideEffectAsync_UpdatesExistingLegacyPolicies(
        [PolicyUpdate(PolicyType.SendControls, enabled: true)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SendControls, enabled: true)] Policy postUpsertedPolicy,
        [Policy(PolicyType.DisableSend, enabled: false)] Policy existingDisableSendPolicy,
        [Policy(PolicyType.SendOptions, enabled: false)] Policy existingSendOptionsPolicy,
        SutProvider<SendControlsSyncPolicyEvent> sutProvider)
    {
        postUpsertedPolicy.OrganizationId = policyUpdate.OrganizationId;
        existingDisableSendPolicy.OrganizationId = policyUpdate.OrganizationId;
        existingSendOptionsPolicy.OrganizationId = policyUpdate.OrganizationId;
        postUpsertedPolicy.SetDataModel(new SendControlsPolicyData { DisableSend = true, DisableHideEmail = true });

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.DisableSend)
            .Returns(existingDisableSendPolicy);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.SendOptions)
            .Returns(existingSendOptionsPolicy);

        await sutProvider.Sut.ExecutePostUpsertSideEffectAsync(
            new SavePolicyModel(policyUpdate), postUpsertedPolicy, null);

        await sutProvider.GetDependency<IPolicyRepository>()
            .Received(1)
            .UpsertAsync(Arg.Is<Policy>(p =>
                p.Id == existingDisableSendPolicy.Id &&
                p.Enabled == true));
        await sutProvider.GetDependency<IPolicyRepository>()
            .Received(1)
            .UpsertAsync(Arg.Is<Policy>(p =>
                p.Id == existingSendOptionsPolicy.Id &&
                p.Enabled == true));
    }

    [Theory, BitAutoData]
    public async Task ExecutePostUpsertSideEffectAsync_DisablesNonCompliantSends(
        [PolicyUpdate(PolicyType.SendControls, enabled: true)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SendControls, enabled: true)] Policy postUpsertedPolicy,
        [Policy(PolicyType.DisableSend, enabled: false)] Policy existingDisableSendPolicy,
        [Policy(PolicyType.SendOptions, enabled: false)] Policy existingSendOptionsPolicy,
        SutProvider<SendControlsSyncPolicyEvent> sutProvider)
    {
        postUpsertedPolicy.OrganizationId = policyUpdate.OrganizationId;
        existingDisableSendPolicy.OrganizationId = policyUpdate.OrganizationId;
        existingSendOptionsPolicy.OrganizationId = policyUpdate.OrganizationId;
        postUpsertedPolicy.SetDataModel(new SendControlsPolicyData { DisableHideEmail = true, WhoCanAccess = SendWhoCanAccessType.SpecificPeople, AllowedDomains = "duckdodgers.com" });

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.DisableSend)
            .Returns(existingDisableSendPolicy);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.SendOptions)
            .Returns(existingSendOptionsPolicy);
        var orgUserId = Guid.NewGuid();
        var orgUser = new OrganizationUser
        {
            UserId = orgUserId
        };
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByOrganizationAsync(postUpsertedPolicy.OrganizationId, null)
            .Returns([ orgUser ]);
        var nonCompliantSend1 = new Send
        {
            Id = Guid.NewGuid(),
            HideEmail = true
        };
        var nonCompliantSend2 = new Send
        {
            Id = Guid.NewGuid(),
            AuthType = AuthType.Email,
            Emails = "marvin@mars.planet"
        };
        sutProvider.GetDependency<ISendRepository>()
            .GetManyByUserIdAsync(orgUserId)
            .Returns([ nonCompliantSend1, nonCompliantSend2 ]);

        await sutProvider.Sut.ExecutePostUpsertSideEffectAsync(
            new SavePolicyModel(policyUpdate), postUpsertedPolicy, null);
        
        await sutProvider.GetDependency<ISendRepository>()
            .Received(1)
            .UpdateManyDisabledAsync(Arg.Is<List<Guid>>(l => l.Count() == 2), true);
    }

    [Theory, BitAutoData]
    public async Task ExecutePostUpsertSideEffectAsync_EnablesCompliantSends(
        [PolicyUpdate(PolicyType.SendControls, enabled: true)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SendControls, enabled: true)] Policy postUpsertedPolicy,
        [Policy(PolicyType.DisableSend, enabled: false)] Policy existingDisableSendPolicy,
        [Policy(PolicyType.SendOptions, enabled: false)] Policy existingSendOptionsPolicy,
        SutProvider<SendControlsSyncPolicyEvent> sutProvider)
    {
        postUpsertedPolicy.OrganizationId = policyUpdate.OrganizationId;
        existingDisableSendPolicy.OrganizationId = policyUpdate.OrganizationId;
        existingSendOptionsPolicy.OrganizationId = policyUpdate.OrganizationId;
        postUpsertedPolicy.SetDataModel(new SendControlsPolicyData { DisableHideEmail = true, WhoCanAccess = SendWhoCanAccessType.SpecificPeople, AllowedDomains = "duckdodgers.com" });

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.DisableSend)
            .Returns(existingDisableSendPolicy);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.SendOptions)
            .Returns(existingSendOptionsPolicy);
        var orgUserId = Guid.NewGuid();
        var orgUser = new OrganizationUser
        {
            UserId = orgUserId
        };
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByOrganizationAsync(policyUpdate.OrganizationId, null)
            .Returns([ orgUser ]);

        var compliantSend1 = new Send
        {
            AuthType = AuthType.Email,
            Emails = "daffy@duckdodgers.com"
        };
        sutProvider.GetDependency<ISendRepository>()
            .GetManyByUserIdAsync(orgUserId)
            .Returns([ compliantSend1 ]);

        await sutProvider.Sut.ExecutePostUpsertSideEffectAsync(
            new SavePolicyModel(policyUpdate), postUpsertedPolicy, null);
        
        await sutProvider.GetDependency<ISendRepository>()
            .Received(1)
            .UpdateManyDisabledAsync(Arg.Is<List<Guid>>(l => l.Count() == 1), false);
    }
}
