using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyEventHandlers;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bitwarden.Server.Sdk.Features;
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
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.SendControlsExistingSends)
            .Returns(true);

        var previouslyDisabledSend1 = new Send
        {
            Id = Guid.NewGuid(),
            AuthType = AuthType.None,
            Disabled = true,
        };
        var previouslyDisabledSend2 = new Send
        {
            Id = Guid.NewGuid(),
            AuthType = AuthType.Email,
            Disabled = true,
        };
        var sendIds = new List<Guid>([previouslyDisabledSend1.Id, previouslyDisabledSend2.Id]);
        sutProvider.GetDependency<ISendRepository>()
            .GetIdsByOrganizationIdAsync(policyUpdate.OrganizationId)
            .Returns(sendIds);
        sutProvider.GetDependency<ISendRepository>()
            .GetManyByIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([previouslyDisabledSend1, previouslyDisabledSend2]);

        await sutProvider.Sut.ExecutePostUpsertSideEffectAsync(
            new SavePolicyModel(policyUpdate), postUpsertedPolicy, null);

        await sutProvider.GetDependency<ISendRepository>()
            .Received(1)
            .UpdateManyDisabledAsync(Arg.Is<List<Guid>>(l => l.Count() == 2 && l.Contains(previouslyDisabledSend1.Id) && l.Contains(previouslyDisabledSend2.Id)), false);
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
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.SendControlsExistingSends)
            .Returns(true);

        var otherwiseCompliantSend1 = new Send
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            AuthType = AuthType.None,
        };
        var otherwiseCompliantSend2 = new Send
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            AuthType = AuthType.Password,
        };
        var sendIds = new List<Guid>([otherwiseCompliantSend1.Id, otherwiseCompliantSend2.Id]);
        sutProvider.GetDependency<ISendRepository>()
            .GetIdsByOrganizationIdAsync(policyUpdate.OrganizationId)
            .Returns(sendIds);
        sutProvider.GetDependency<ISendRepository>()
            .GetManyByIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([otherwiseCompliantSend1, otherwiseCompliantSend2]);

        await sutProvider.Sut.ExecutePostUpsertSideEffectAsync(
            new SavePolicyModel(policyUpdate), postUpsertedPolicy, null);

        await sutProvider.GetDependency<ISendRepository>()
            .Received(1)
            .UpdateManyDisabledAsync(Arg.Is<List<Guid>>(l => l.Count() == 2 && l.Contains(otherwiseCompliantSend1.Id) && l.Contains(otherwiseCompliantSend2.Id)), true);
        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogSendEventsAsync(
                Arg.Is<IEnumerable<(Send send, EventType type)>>(events =>
                    events.Count() == 2
                    && events.All(e => e.type == EventType.Send_PolicyDisabled)
                    && events.Any(e => e.send.Id == otherwiseCompliantSend1.Id)
                    && events.Any(e => e.send.Id == otherwiseCompliantSend2.Id)),
                policyUpdate.OrganizationId);
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
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.SendControlsExistingSends)
            .Returns(true);

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
        var sendIds = new List<Guid>([compliantSend.Id, nonCompliantSend.Id]);
        sutProvider.GetDependency<ISendRepository>()
            .GetIdsByOrganizationIdAsync(policyUpdate.OrganizationId)
            .Returns(sendIds);
        sutProvider.GetDependency<ISendRepository>()
            .GetManyByIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([compliantSend, nonCompliantSend]);

        await sutProvider.Sut.ExecutePostUpsertSideEffectAsync(
            new SavePolicyModel(policyUpdate), postUpsertedPolicy, null);

        await sutProvider.GetDependency<ISendRepository>()
            .DidNotReceive()
            .UpdateManyDisabledAsync(Arg.Is<List<Guid>>(l => l.Contains(compliantSend.Id)), Arg.Any<bool>());
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
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.SendControlsExistingSends)
            .Returns(true);

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
        var sendIds = new List<Guid>([compliantSend.Id, nonCompliantSend1.Id, nonCompliantSend2.Id]);
        sutProvider.GetDependency<ISendRepository>()
            .GetIdsByOrganizationIdAsync(policyUpdate.OrganizationId)
            .Returns(sendIds);
        sutProvider.GetDependency<ISendRepository>()
            .GetManyByIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([compliantSend, nonCompliantSend1, nonCompliantSend2]);

        await sutProvider.Sut.ExecutePostUpsertSideEffectAsync(
            new SavePolicyModel(policyUpdate), postUpsertedPolicy, null);

        await sutProvider.GetDependency<ISendRepository>()
            .DidNotReceive()
            .UpdateManyDisabledAsync(Arg.Is<List<Guid>>(l => l.Contains(compliantSend.Id)), Arg.Any<bool>());
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
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.SendControlsExistingSends)
            .Returns(true);

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
        var sendIds = new List<Guid>([compliantSend.Id, nonCompliantSend1.Id, nonCompliantSend2.Id]);
        sutProvider.GetDependency<ISendRepository>()
            .GetIdsByOrganizationIdAsync(policyUpdate.OrganizationId)
            .Returns(sendIds);
        sutProvider.GetDependency<ISendRepository>()
            .GetManyByIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([compliantSend, nonCompliantSend1, nonCompliantSend2]);

        await sutProvider.Sut.ExecutePostUpsertSideEffectAsync(
            new SavePolicyModel(policyUpdate), postUpsertedPolicy, null);

        await sutProvider.GetDependency<ISendRepository>()
            .DidNotReceive()
            .UpdateManyDisabledAsync(Arg.Is<List<Guid>>(l => l.Contains(compliantSend.Id)), Arg.Any<bool>());
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
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.SendControlsExistingSends)
            .Returns(true);

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
        var sendIds = new List<Guid>([compliantSend.Id, nonCompliantSend1.Id, nonCompliantSend2.Id, nonCompliantSend3.Id]);
        sutProvider.GetDependency<ISendRepository>()
            .GetIdsByOrganizationIdAsync(policyUpdate.OrganizationId)
            .Returns(sendIds);
        sutProvider.GetDependency<ISendRepository>()
            .GetManyByIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([compliantSend, nonCompliantSend1, nonCompliantSend2, nonCompliantSend3]);

        await sutProvider.Sut.ExecutePostUpsertSideEffectAsync(
            new SavePolicyModel(policyUpdate), postUpsertedPolicy, null);

        await sutProvider.GetDependency<ISendRepository>()
            .DidNotReceive()
            .UpdateManyDisabledAsync(Arg.Is<List<Guid>>(l => l.Contains(compliantSend.Id)), Arg.Any<bool>());
        await sutProvider.GetDependency<ISendRepository>()
            .Received(1)
            .UpdateManyDisabledAsync(Arg.Is<List<Guid>>(l => l.Count == 3 && l.Contains(nonCompliantSend1.Id) && l.Contains(nonCompliantSend2.Id) && l.Contains(nonCompliantSend3.Id)), true);
    }

    [Theory, BitAutoData]
    public async Task ExecutePostUpsertSideEffectAsync_AllowedSendTypesDisablesNoncompliantSends(
        [PolicyUpdate(PolicyType.SendControls, enabled: true)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SendControls, enabled: true)] Policy postUpsertedPolicy,
        [Policy(PolicyType.DisableSend, enabled: false)] Policy existingDisableSendPolicy,
        [Policy(PolicyType.SendOptions, enabled: false)] Policy existingSendOptionsPolicy,
        SutProvider<SendControlsSyncPolicyEvent> sutProvider)
    {
        postUpsertedPolicy.OrganizationId = policyUpdate.OrganizationId;
        existingDisableSendPolicy.OrganizationId = policyUpdate.OrganizationId;
        existingSendOptionsPolicy.OrganizationId = policyUpdate.OrganizationId;
        postUpsertedPolicy.SetDataModel(new SendControlsPolicyData { AllowedSendTypes = [SendType.Text] });

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.DisableSend)
            .Returns(existingDisableSendPolicy);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.SendOptions)
            .Returns(existingSendOptionsPolicy);
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.SendControlsExistingSends)
            .Returns(true);

        var compliantSend = new Send
        {
            Id = Guid.NewGuid(),
            Type = SendType.Text
        };
        var nonCompliantSend = new Send
        {
            Id = Guid.NewGuid(),
            Type = SendType.File
        };
        var sendIds = new List<Guid>([compliantSend.Id, nonCompliantSend.Id]);
        sutProvider.GetDependency<ISendRepository>()
            .GetIdsByOrganizationIdAsync(policyUpdate.OrganizationId)
            .Returns(sendIds);
        sutProvider.GetDependency<ISendRepository>()
            .GetManyByIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([compliantSend, nonCompliantSend]);

        await sutProvider.Sut.ExecutePostUpsertSideEffectAsync(
            new SavePolicyModel(policyUpdate), postUpsertedPolicy, null);

        await sutProvider.GetDependency<ISendRepository>()
            .DidNotReceive()
            .UpdateManyDisabledAsync(Arg.Is<List<Guid>>(l => l.Contains(compliantSend.Id)), Arg.Any<bool>());
        await sutProvider.GetDependency<ISendRepository>()
            .Received(1)
            .UpdateManyDisabledAsync(Arg.Is<List<Guid>>(l => l.Count() == 1 && l.ElementAt(0) == nonCompliantSend.Id), true);
    }

    [Theory, BitAutoData]
    public async Task ExecutePostUpsertSideEffectAsync_DeletionHoursDisablesNoncompliantSends(
        [PolicyUpdate(PolicyType.SendControls, enabled: true)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SendControls, enabled: true)] Policy postUpsertedPolicy,
        [Policy(PolicyType.DisableSend, enabled: false)] Policy existingDisableSendPolicy,
        [Policy(PolicyType.SendOptions, enabled: false)] Policy existingSendOptionsPolicy,
        SutProvider<SendControlsSyncPolicyEvent> sutProvider)
    {
        postUpsertedPolicy.OrganizationId = policyUpdate.OrganizationId;
        existingDisableSendPolicy.OrganizationId = policyUpdate.OrganizationId;
        existingSendOptionsPolicy.OrganizationId = policyUpdate.OrganizationId;
        postUpsertedPolicy.SetDataModel(new SendControlsPolicyData { DeletionHours = 72 });

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.DisableSend)
            .Returns(existingDisableSendPolicy);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.SendOptions)
            .Returns(existingSendOptionsPolicy);
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.SendControlsExistingSends)
            .Returns(true);

        var twoDaysAgo = DateTime.UtcNow.AddDays(-2);
        var nonCompliantSend = new Send
        {
            Id = Guid.NewGuid(),
            CreationDate = twoDaysAgo,
            DeletionDate = twoDaysAgo.AddDays(7)
        };
        var compliantSend = new Send
        {
            Id = Guid.NewGuid(),
            CreationDate = twoDaysAgo,
            DeletionDate = twoDaysAgo.AddDays(3)
        };
        var sendIds = new List<Guid>([nonCompliantSend.Id, compliantSend.Id]);
        sutProvider.GetDependency<ISendRepository>()
            .GetIdsByOrganizationIdAsync(policyUpdate.OrganizationId)
            .Returns(sendIds);
        sutProvider.GetDependency<ISendRepository>()
            .GetManyByIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([nonCompliantSend, compliantSend]);

        await sutProvider.Sut.ExecutePostUpsertSideEffectAsync(
            new SavePolicyModel(policyUpdate), postUpsertedPolicy, null);

        await sutProvider.GetDependency<ISendRepository>()
            .DidNotReceive()
            .UpdateManyDisabledAsync(Arg.Is<List<Guid>>(l => l.Contains(compliantSend.Id)), Arg.Any<bool>());
        await sutProvider.GetDependency<ISendRepository>()
            .Received(1)
            .UpdateManyDisabledAsync(Arg.Is<List<Guid>>(l => l.Count() == 1 && l.ElementAt(0) == nonCompliantSend.Id), true);
    }

    [Theory, BitAutoData]
    public async Task ExecutePostUpsertSideEffectAsync_IgnoresOwnersAndAdminsNonCompliantSends(
        [PolicyUpdate(PolicyType.SendControls, enabled: true)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SendControls, enabled: true)] Policy postUpsertedPolicy,
        [Policy(PolicyType.DisableSend, enabled: false)] Policy existingDisableSendPolicy,
        [Policy(PolicyType.SendOptions, enabled: false)] Policy existingSendOptionsPolicy,
        SutProvider<SendControlsSyncPolicyEvent> sutProvider)
    {
        postUpsertedPolicy.OrganizationId = policyUpdate.OrganizationId;
        existingDisableSendPolicy.OrganizationId = policyUpdate.OrganizationId;
        existingSendOptionsPolicy.OrganizationId = policyUpdate.OrganizationId;
        postUpsertedPolicy.SetDataModel(new SendControlsPolicyData { AllowedSendTypes = [SendType.Text] });

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.DisableSend)
            .Returns(existingDisableSendPolicy);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.SendOptions)
            .Returns(existingSendOptionsPolicy);
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.SendControlsExistingSends)
            .Returns(true);

        var adminOrganizationUser = new OrganizationUserUserDetails
        {
            UserId = Guid.NewGuid(),
            Type = Enums.OrganizationUserType.Admin,
        };
        var adminNoncompliantSend = new Send
        {
            Id = Guid.NewGuid(),
            Type = SendType.File,
            UserId = adminOrganizationUser.UserId,
        };
        var ownerOrganizationUser = new OrganizationUserUserDetails
        {
            UserId = Guid.NewGuid(),
            Type = Enums.OrganizationUserType.Owner,
        };
        var ownerNoncompliantSend = new Send
        {
            Id = Guid.NewGuid(),
            Type = SendType.File,
            UserId = ownerOrganizationUser.UserId,
        };
        var sendIds = new List<Guid>([adminNoncompliantSend.Id, ownerNoncompliantSend.Id]);
        sutProvider.GetDependency<ISendRepository>()
            .GetIdsByOrganizationIdAsync(policyUpdate.OrganizationId)
            .Returns(sendIds);
        sutProvider.GetDependency<ISendRepository>()
            .GetManyByIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([adminNoncompliantSend, ownerNoncompliantSend]);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByMinimumRoleAsync(policyUpdate.OrganizationId, Enums.OrganizationUserType.Admin)
            .Returns([adminOrganizationUser, ownerOrganizationUser]);

        await sutProvider.Sut.ExecutePostUpsertSideEffectAsync(
            new SavePolicyModel(policyUpdate), postUpsertedPolicy, null);

        await sutProvider.GetDependency<ISendRepository>()
            .DidNotReceive()
            .UpdateManyDisabledAsync(Arg.Any<List<Guid>>(), Arg.Any<bool>());
    }

    [Theory, BitAutoData]
    public async Task ExecutePostUpsertSideEffectAsync_ReEnablesDisabledSendThatIsNowCompliant(
        [PolicyUpdate(PolicyType.SendControls, enabled: true)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SendControls, enabled: true)] Policy postUpsertedPolicy,
        [Policy(PolicyType.DisableSend, enabled: false)] Policy existingDisableSendPolicy,
        [Policy(PolicyType.SendOptions, enabled: false)] Policy existingSendOptionsPolicy,
        SutProvider<SendControlsSyncPolicyEvent> sutProvider)
    {
        postUpsertedPolicy.OrganizationId = policyUpdate.OrganizationId;
        existingDisableSendPolicy.OrganizationId = policyUpdate.OrganizationId;
        existingSendOptionsPolicy.OrganizationId = policyUpdate.OrganizationId;
        // A policy that only restricts AllowedSendTypes to File; the Send below is compliant.
        postUpsertedPolicy.SetDataModel(new SendControlsPolicyData { AllowedSendTypes = [SendType.File] });

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.DisableSend)
            .Returns(existingDisableSendPolicy);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.SendOptions)
            .Returns(existingSendOptionsPolicy);
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.SendControlsExistingSends)
            .Returns(true);

        // There is no mechanism to distinguish a Send the policy previously disabled from one its
        // owner disabled themselves. Without that provenance, a relaxed (but still enabled) policy
        // must re-enable any currently-disabled Send that is now compliant, or a Send disabled while
        // the policy was stricter would stay disabled forever even after the policy no longer
        // condemns it and the member never left the org.
        var disabledCompliantSend = new Send
        {
            Id = Guid.NewGuid(),
            Type = SendType.File,
            Disabled = true,
        };
        var sendIds = new List<Guid>([disabledCompliantSend.Id]);
        sutProvider.GetDependency<ISendRepository>()
            .GetIdsByOrganizationIdAsync(policyUpdate.OrganizationId)
            .Returns(sendIds);
        sutProvider.GetDependency<ISendRepository>()
            .GetManyByIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([disabledCompliantSend]);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByMinimumRoleAsync(policyUpdate.OrganizationId, Enums.OrganizationUserType.Admin)
            .Returns([]);

        await sutProvider.Sut.ExecutePostUpsertSideEffectAsync(
            new SavePolicyModel(policyUpdate), postUpsertedPolicy, null);

        await sutProvider.GetDependency<ISendRepository>()
            .Received(1)
            .UpdateManyDisabledAsync(Arg.Is<List<Guid>>(l => l.Contains(disabledCompliantSend.Id)), false);
    }

    [Theory, BitAutoData]
    public async Task ExecutePostUpsertSideEffectAsync_PolicyDisabled_StillReEnablesPreviouslyDisabledSends(
        [PolicyUpdate(PolicyType.SendControls, enabled: false)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SendControls, enabled: false)] Policy postUpsertedPolicy,
        [Policy(PolicyType.DisableSend, enabled: false)] Policy existingDisableSendPolicy,
        [Policy(PolicyType.SendOptions, enabled: false)] Policy existingSendOptionsPolicy,
        SutProvider<SendControlsSyncPolicyEvent> sutProvider)
    {
        postUpsertedPolicy.OrganizationId = policyUpdate.OrganizationId;
        existingDisableSendPolicy.OrganizationId = policyUpdate.OrganizationId;
        existingSendOptionsPolicy.OrganizationId = policyUpdate.OrganizationId;
        postUpsertedPolicy.Enabled = false;
        postUpsertedPolicy.SetDataModel(new SendControlsPolicyData());

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.DisableSend)
            .Returns(existingDisableSendPolicy);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.SendOptions)
            .Returns(existingSendOptionsPolicy);
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.SendControlsExistingSends)
            .Returns(true);

        var previouslyDisabledSend = new Send { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Disabled = true };
        var sendIds = new List<Guid>([previouslyDisabledSend.Id]);
        sutProvider.GetDependency<ISendRepository>()
            .GetIdsByOrganizationIdAsync(policyUpdate.OrganizationId)
            .Returns(sendIds);
        sutProvider.GetDependency<ISendRepository>()
            .GetManyByIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([previouslyDisabledSend]);

        await sutProvider.Sut.ExecutePostUpsertSideEffectAsync(
            new SavePolicyModel(policyUpdate), postUpsertedPolicy, null);

        await sutProvider.GetDependency<ISendRepository>()
            .Received(1)
            .UpdateManyDisabledAsync(Arg.Is<List<Guid>>(l => l.Contains(previouslyDisabledSend.Id)), false);
        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogSendEventsAsync(
                Arg.Is<IEnumerable<(Send send, EventType type)>>(events =>
                    events.Count() == 1
                    && events.Single().type == EventType.Send_PolicyEnabled
                    && events.Single().send.Id == previouslyDisabledSend.Id),
                policyUpdate.OrganizationId);
    }
}
