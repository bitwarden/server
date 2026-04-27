using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.Repositories;
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
    public async Task ExecutePostUpsertSideEffectAsync_DisablingPolicyEnablesAllSends(
        [PolicyUpdate(PolicyType.SendControls, enabled: true)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SendControls, enabled: true)] Policy postUpsertedPolicy,
        [Policy(PolicyType.DisableSend, enabled: false)] Policy existingDisableSendPolicy,
        [Policy(PolicyType.SendOptions, enabled: false)] Policy existingSendOptionsPolicy,
        SutProvider<SendControlsSyncPolicyEvent> sutProvider)
    {
        postUpsertedPolicy.OrganizationId = policyUpdate.OrganizationId;
        existingDisableSendPolicy.OrganizationId = policyUpdate.OrganizationId;
        existingSendOptionsPolicy.OrganizationId = policyUpdate.OrganizationId;
        postUpsertedPolicy.SetDataModel(new SendControlsPolicyData { WhoCanAccess = SendWhoCanAccessType.PasswordProtected });
        postUpsertedPolicy.Enabled = false;

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.DisableSend)
            .Returns(existingDisableSendPolicy);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.SendOptions)
            .Returns(existingSendOptionsPolicy);

        var nonCompliantSend1 = new Send
        {
            Id = Guid.NewGuid(),
            AuthType = AuthType.None,
        };
        var nonCompliantSend2 = new Send
        {
            Id = Guid.NewGuid(),
            AuthType = AuthType.Email,
        };
        var sendIds = new List<Guid>([ nonCompliantSend1.Id, nonCompliantSend2.Id ]);
        sutProvider.GetDependency<ISendRepository>()
            .GetIdsByOrganizationIdAsync(policyUpdate.OrganizationId)
            .Returns(sendIds);
        sutProvider.GetDependency<ISendRepository>()
            .GetManyByIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([ nonCompliantSend1, nonCompliantSend2 ]);

        await sutProvider.Sut.ExecutePostUpsertSideEffectAsync(
            new SavePolicyModel(policyUpdate), postUpsertedPolicy, null);
        
        await sutProvider.GetDependency<ISendRepository>()
            .Received(1)
            .UpdateManyDisabledAsync(Arg.Is<List<Guid>>(l => l.Count() == 2 && l.Contains(nonCompliantSend1.Id) && l.Contains(nonCompliantSend2.Id)), false);
    }

    [Theory, BitAutoData]
    public async Task ExecutePostUpsertSideEffectAsync_DisableSendDisablesAllSends(
        [PolicyUpdate(PolicyType.SendControls, enabled: true)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SendControls, enabled: true)] Policy postUpsertedPolicy,
        [Policy(PolicyType.DisableSend, enabled: false)] Policy existingDisableSendPolicy,
        [Policy(PolicyType.SendOptions, enabled: false)] Policy existingSendOptionsPolicy,
        SutProvider<SendControlsSyncPolicyEvent> sutProvider)
    {
        postUpsertedPolicy.OrganizationId = policyUpdate.OrganizationId;
        existingDisableSendPolicy.OrganizationId = policyUpdate.OrganizationId;
        existingSendOptionsPolicy.OrganizationId = policyUpdate.OrganizationId;
        postUpsertedPolicy.SetDataModel(new SendControlsPolicyData { DisableSend = true });

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.DisableSend)
            .Returns(existingDisableSendPolicy);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.SendOptions)
            .Returns(existingSendOptionsPolicy);

        var otherwiseCompliantSend1 = new Send
        {
            Id = Guid.NewGuid(),
            AuthType = AuthType.None,
        };
        var otherwiseCompliantSend2 = new Send
        {
            Id = Guid.NewGuid(),
            AuthType = AuthType.Password,
        };
        var sendIds = new List<Guid>([ otherwiseCompliantSend1.Id, otherwiseCompliantSend2.Id ]);
        sutProvider.GetDependency<ISendRepository>()
            .GetIdsByOrganizationIdAsync(policyUpdate.OrganizationId)
            .Returns(sendIds);
        sutProvider.GetDependency<ISendRepository>()
            .GetManyByIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([ otherwiseCompliantSend1, otherwiseCompliantSend2 ]);

        await sutProvider.Sut.ExecutePostUpsertSideEffectAsync(
            new SavePolicyModel(policyUpdate), postUpsertedPolicy, null);
        
        await sutProvider.GetDependency<ISendRepository>()
            .Received(1)
            .UpdateManyDisabledAsync(Arg.Is<List<Guid>>(l => l.Count() == 2 && l.Contains(otherwiseCompliantSend1.Id) && l.Contains(otherwiseCompliantSend2.Id)), true);
    }

    [Theory, BitAutoData]
    public async Task ExecutePostUpsertSideEffectAsync_DisableHideEmailDisablesRelevantSends(
        [PolicyUpdate(PolicyType.SendControls, enabled: true)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SendControls, enabled: true)] Policy postUpsertedPolicy,
        [Policy(PolicyType.DisableSend, enabled: false)] Policy existingDisableSendPolicy,
        [Policy(PolicyType.SendOptions, enabled: false)] Policy existingSendOptionsPolicy,
        SutProvider<SendControlsSyncPolicyEvent> sutProvider)
    {
        postUpsertedPolicy.OrganizationId = policyUpdate.OrganizationId;
        existingDisableSendPolicy.OrganizationId = policyUpdate.OrganizationId;
        existingSendOptionsPolicy.OrganizationId = policyUpdate.OrganizationId;
        postUpsertedPolicy.SetDataModel(new SendControlsPolicyData { DisableHideEmail = true });

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.DisableSend)
            .Returns(existingDisableSendPolicy);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.SendOptions)
            .Returns(existingSendOptionsPolicy);

        var compliantSend = new Send
        {
            Id = Guid.NewGuid(),
            AuthType = AuthType.None,
        };
        var nonCompliantSend = new Send
        {
            Id = Guid.NewGuid(),
            HideEmail = true
        };
        var sendIds = new List<Guid>([ compliantSend.Id, nonCompliantSend.Id ]);
        sutProvider.GetDependency<ISendRepository>()
            .GetIdsByOrganizationIdAsync(policyUpdate.OrganizationId)
            .Returns(sendIds);
        sutProvider.GetDependency<ISendRepository>()
            .GetManyByIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([ compliantSend, nonCompliantSend ]);

        await sutProvider.Sut.ExecutePostUpsertSideEffectAsync(
            new SavePolicyModel(policyUpdate), postUpsertedPolicy, null);
        
        await sutProvider.GetDependency<ISendRepository>()
            .Received(1)
            .UpdateManyDisabledAsync(Arg.Is<List<Guid>>(l => l.Count == 1 && l.Contains(compliantSend.Id)), false);
        await sutProvider.GetDependency<ISendRepository>()
            .Received(1)
            .UpdateManyDisabledAsync(Arg.Is<List<Guid>>(l => l.Count == 1 && l.Contains(nonCompliantSend.Id)), true);
    }

    [Theory, BitAutoData]
    public async Task ExecutePostUpsertSideEffectAsync_AuthTypePasswordDisablesRelevantSends(
        [PolicyUpdate(PolicyType.SendControls, enabled: true)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SendControls, enabled: true)] Policy postUpsertedPolicy,
        [Policy(PolicyType.DisableSend, enabled: false)] Policy existingDisableSendPolicy,
        [Policy(PolicyType.SendOptions, enabled: false)] Policy existingSendOptionsPolicy,
        SutProvider<SendControlsSyncPolicyEvent> sutProvider)
    {
        postUpsertedPolicy.OrganizationId = policyUpdate.OrganizationId;
        existingDisableSendPolicy.OrganizationId = policyUpdate.OrganizationId;
        existingSendOptionsPolicy.OrganizationId = policyUpdate.OrganizationId;
        postUpsertedPolicy.SetDataModel(new SendControlsPolicyData { WhoCanAccess = SendWhoCanAccessType.PasswordProtected });

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.DisableSend)
            .Returns(existingDisableSendPolicy);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.SendOptions)
            .Returns(existingSendOptionsPolicy);

        var compliantSend = new Send
        {
            Id = Guid.NewGuid(),
            AuthType = AuthType.Password,
        };
        var nonCompliantSend1 = new Send
        {
            Id = Guid.NewGuid(),
            AuthType = AuthType.None
        };
        var nonCompliantSend2 = new Send
        {
            Id = Guid.NewGuid(),
            AuthType = AuthType.Email
        };
        var sendIds = new List<Guid>([ compliantSend.Id, nonCompliantSend1.Id, nonCompliantSend2.Id ]);
        sutProvider.GetDependency<ISendRepository>()
            .GetIdsByOrganizationIdAsync(policyUpdate.OrganizationId)
            .Returns(sendIds);
        sutProvider.GetDependency<ISendRepository>()
            .GetManyByIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([ compliantSend, nonCompliantSend1, nonCompliantSend2 ]);

        await sutProvider.Sut.ExecutePostUpsertSideEffectAsync(
            new SavePolicyModel(policyUpdate), postUpsertedPolicy, null);
        
        await sutProvider.GetDependency<ISendRepository>()
            .Received(1)
            .UpdateManyDisabledAsync(Arg.Is<List<Guid>>(l => l.Count == 1 && l.Contains(compliantSend.Id)), false);
        await sutProvider.GetDependency<ISendRepository>()
            .Received(1)
            .UpdateManyDisabledAsync(Arg.Is<List<Guid>>(l => l.Count == 2 && l.Contains(nonCompliantSend1.Id) && l.Contains(nonCompliantSend2.Id)), true);
    }

    [Theory, BitAutoData]
    public async Task ExecutePostUpsertSideEffectAsync_AuthTypeEmailNoDomainDisablesRelevantSends(
        [PolicyUpdate(PolicyType.SendControls, enabled: true)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SendControls, enabled: true)] Policy postUpsertedPolicy,
        [Policy(PolicyType.DisableSend, enabled: false)] Policy existingDisableSendPolicy,
        [Policy(PolicyType.SendOptions, enabled: false)] Policy existingSendOptionsPolicy,
        SutProvider<SendControlsSyncPolicyEvent> sutProvider)
    {
        postUpsertedPolicy.OrganizationId = policyUpdate.OrganizationId;
        existingDisableSendPolicy.OrganizationId = policyUpdate.OrganizationId;
        existingSendOptionsPolicy.OrganizationId = policyUpdate.OrganizationId;
        postUpsertedPolicy.SetDataModel(new SendControlsPolicyData { WhoCanAccess = SendWhoCanAccessType.SpecificPeople });

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.DisableSend)
            .Returns(existingDisableSendPolicy);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.SendOptions)
            .Returns(existingSendOptionsPolicy);

        var compliantSend = new Send
        {
            Id = Guid.NewGuid(),
            AuthType = AuthType.Email,
        };
        var nonCompliantSend1 = new Send
        {
            Id = Guid.NewGuid(),
            AuthType = AuthType.None
        };
        var nonCompliantSend2 = new Send
        {
            Id = Guid.NewGuid(),
            AuthType = AuthType.Password
        };
        var sendIds = new List<Guid>([ compliantSend.Id, nonCompliantSend1.Id, nonCompliantSend2.Id ]);
        sutProvider.GetDependency<ISendRepository>()
            .GetIdsByOrganizationIdAsync(policyUpdate.OrganizationId)
            .Returns(sendIds);
        sutProvider.GetDependency<ISendRepository>()
            .GetManyByIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([ compliantSend, nonCompliantSend1, nonCompliantSend2 ]);

        await sutProvider.Sut.ExecutePostUpsertSideEffectAsync(
            new SavePolicyModel(policyUpdate), postUpsertedPolicy, null);
        
        await sutProvider.GetDependency<ISendRepository>()
            .Received(1)
            .UpdateManyDisabledAsync(Arg.Is<List<Guid>>(l => l.Count == 1 && l.Contains(compliantSend.Id)), false);
        await sutProvider.GetDependency<ISendRepository>()
            .Received(1)
            .UpdateManyDisabledAsync(Arg.Is<List<Guid>>(l => l.Count == 2 && l.Contains(nonCompliantSend1.Id) && l.Contains(nonCompliantSend2.Id)), true);
    }

[Theory, BitAutoData]
    public async Task ExecutePostUpsertSideEffectAsync_AuthTypeEmailWithDomainDisablesRelevantSends(
        [PolicyUpdate(PolicyType.SendControls, enabled: true)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SendControls, enabled: true)] Policy postUpsertedPolicy,
        [Policy(PolicyType.DisableSend, enabled: false)] Policy existingDisableSendPolicy,
        [Policy(PolicyType.SendOptions, enabled: false)] Policy existingSendOptionsPolicy,
        SutProvider<SendControlsSyncPolicyEvent> sutProvider)
    {
        postUpsertedPolicy.OrganizationId = policyUpdate.OrganizationId;
        existingDisableSendPolicy.OrganizationId = policyUpdate.OrganizationId;
        existingSendOptionsPolicy.OrganizationId = policyUpdate.OrganizationId;
        postUpsertedPolicy.SetDataModel(new SendControlsPolicyData { WhoCanAccess = SendWhoCanAccessType.SpecificPeople, AllowedDomains = "duckdodgers.com" });

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.DisableSend)
            .Returns(existingDisableSendPolicy);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.SendOptions)
            .Returns(existingSendOptionsPolicy);

        var compliantSend = new Send
        {
            Id = Guid.NewGuid(),
            AuthType = AuthType.Email,
            Emails = "daffy@duckdodgers.com"
        };
        var nonCompliantSend1 = new Send
        {
            Id = Guid.NewGuid(),
            AuthType = AuthType.None
        };
        var nonCompliantSend2 = new Send
        {
            Id = Guid.NewGuid(),
            AuthType = AuthType.Password
        };
        var nonCompliantSend3 = new Send
        {
            Id = Guid.NewGuid(),
            AuthType = AuthType.Email,
            Emails = "marvin@mars.planet"
        };
        var sendIds = new List<Guid>([ compliantSend.Id, nonCompliantSend1.Id, nonCompliantSend2.Id, nonCompliantSend3.Id ]);
        sutProvider.GetDependency<ISendRepository>()
            .GetIdsByOrganizationIdAsync(policyUpdate.OrganizationId)
            .Returns(sendIds);
        sutProvider.GetDependency<ISendRepository>()
            .GetManyByIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([ compliantSend, nonCompliantSend1, nonCompliantSend2, nonCompliantSend3 ]);

        await sutProvider.Sut.ExecutePostUpsertSideEffectAsync(
            new SavePolicyModel(policyUpdate), postUpsertedPolicy, null);
        
        await sutProvider.GetDependency<ISendRepository>()
            .Received(1)
            .UpdateManyDisabledAsync(Arg.Is<List<Guid>>(l => l.Count == 1 && l.Contains(compliantSend.Id)), false);
        await sutProvider.GetDependency<ISendRepository>()
            .Received(1)
            .UpdateManyDisabledAsync(Arg.Is<List<Guid>>(l => l.Count == 3 && l.Contains(nonCompliantSend1.Id) && l.Contains(nonCompliantSend2.Id) && l.Contains(nonCompliantSend3.Id)), true);
    }

    [Theory, BitAutoData]
    public async Task ExecutePostUpsertSideEffectAsync_DeletionDateUpdatesSendDeletionDates(
        [PolicyUpdate(PolicyType.SendControls, enabled: true)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SendControls, enabled: true)] Policy postUpsertedPolicy,
        [Policy(PolicyType.DisableSend, enabled: false)] Policy existingDisableSendPolicy,
        [Policy(PolicyType.SendOptions, enabled: false)] Policy existingSendOptionsPolicy,
        SutProvider<SendControlsSyncPolicyEvent> sutProvider)
    {
        postUpsertedPolicy.OrganizationId = policyUpdate.OrganizationId;
        existingDisableSendPolicy.OrganizationId = policyUpdate.OrganizationId;
        existingSendOptionsPolicy.OrganizationId = policyUpdate.OrganizationId;
        postUpsertedPolicy.SetDataModel(new SendControlsPolicyData { DeletionDays = 48 });

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.DisableSend)
            .Returns(existingDisableSendPolicy);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.SendOptions)
            .Returns(existingSendOptionsPolicy);

        var send1 = new Send
        {
            Id = Guid.NewGuid(),
            CreationDate = DateTime.UtcNow
        };
        var send2 = new Send
        {
            Id = Guid.NewGuid(),
            CreationDate = DateTime.UtcNow
        };
        var sendIds = new List<Guid>([ send1.Id, send2.Id ]);
        sutProvider.GetDependency<ISendRepository>()
            .GetIdsByOrganizationIdAsync(policyUpdate.OrganizationId)
            .Returns(sendIds);
        sutProvider.GetDependency<ISendRepository>()
            .GetManyByIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([ send1, send2 ]);

        await sutProvider.Sut.ExecutePostUpsertSideEffectAsync(
            new SavePolicyModel(policyUpdate), postUpsertedPolicy, null);
        
        await sutProvider.GetDependency<ISendRepository>()
            .Received(1)
            .UpdateManyDeletionDatesByIdsAsync(Arg.Is<Guid[]>(l => l.Count() == 2), 48);
    }
}
