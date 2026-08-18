using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.OrganizationFeatures.Commands;
using Bit.Services.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Bit.Services.Pam.Test.Commands;

/// <summary>
/// The validation these commands share lives in <see cref="AccessRuleWriteValidator"/> and is covered by
/// AccessRuleWriteValidatorTests; these tests cover persistence, timestamps, and collection association wiring.
/// </summary>
[SutProviderCustomize]
public class CreateAccessRuleCommandTests
{
    private static readonly DateTime _now = new(2026, 5, 21, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task CreateAsync_HappyPath_PersistsWithTimestampsAndValidates(AccessRule rule)
    {
        var sutProvider = SetupSutProvider();
        rule.Name = "VPN + business hours";
        rule.Conditions = """[{"kind":"human_approval"}]""";
        rule.DefaultLeaseDurationSeconds = 3600;
        rule.MaxLeaseDurationSeconds = 28800;
        SetupValidator(sutProvider, rule.OrganizationId, []);
        sutProvider.GetDependency<IAccessRuleRepository>()
            .CreateAsync(rule)
            .Returns(rule);

        var result = await sutProvider.Sut.CreateAsync(rule, []);

        Assert.Equal(_now, result.CreationDate);
        Assert.Equal(_now, result.RevisionDate);
        Assert.Equal(3600, result.DefaultLeaseDurationSeconds);
        Assert.Equal(28800, result.MaxLeaseDurationSeconds);
        await sutProvider.GetDependency<IAccessRuleRepository>().Received(1)
            .CreateAsync(Arg.Is<AccessRule>(r =>
                r.DefaultLeaseDurationSeconds == 3600 && r.MaxLeaseDurationSeconds == 28800));
        // A create has no existing rule to exclude from the validator's uniqueness and conflict checks.
        await sutProvider.GetDependency<IAccessRuleWriteValidator>().Received(1)
            .ValidateAsync(rule.OrganizationId, rule, Arg.Any<IEnumerable<Guid>>(), null);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_WithCollections_AssociatesAndReturnsThem(AccessRule rule, Collection collectionA,
        Collection collectionB)
    {
        var sutProvider = SetupSutProvider();
        rule.Name = "VPN + business hours";
        rule.Conditions = """[{"kind":"human_approval"}]""";
        var collectionIds = new[] { collectionA.Id, collectionB.Id };
        SetupValidator(sutProvider, rule.OrganizationId, [.. collectionIds]);
        sutProvider.GetDependency<IAccessRuleRepository>()
            .CreateAsync(rule)
            .Returns(rule);

        var result = await sutProvider.Sut.CreateAsync(rule, collectionIds);

        Assert.Equal(collectionIds, result.CollectionIds);
        await sutProvider.GetDependency<ICollectionRepository>().Received(1)
            .SetAccessRuleAssociationsAsync(rule.OrganizationId, rule.Id,
                Arg.Is<IEnumerable<Guid>>(ids => ids.OrderBy(x => x).SequenceEqual(collectionIds.OrderBy(x => x))),
                Arg.Is<IEnumerable<Guid>>(ids => !ids.Any()));
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_AllowsExtensionsWithPositiveMax_Persists(AccessRule rule)
    {
        var sutProvider = SetupSutProvider();
        rule.Name = "extendable";
        rule.Conditions = """[{"kind":"human_approval"}]""";
        rule.AllowsExtensions = true;
        rule.MaxExtensionDurationSeconds = 3600;
        SetupValidator(sutProvider, rule.OrganizationId, []);
        sutProvider.GetDependency<IAccessRuleRepository>()
            .CreateAsync(rule)
            .Returns(rule);

        var result = await sutProvider.Sut.CreateAsync(rule, []);

        Assert.True(result.AllowsExtensions);
        Assert.Equal(3600, result.MaxExtensionDurationSeconds);
        await sutProvider.GetDependency<IAccessRuleRepository>().Received(1)
            .CreateAsync(Arg.Is<AccessRule>(r => r.AllowsExtensions && r.MaxExtensionDurationSeconds == 3600));
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_ValidationFails_DoesNotPersist(AccessRule rule)
    {
        var sutProvider = SetupSutProvider();
        sutProvider.GetDependency<IAccessRuleWriteValidator>()
            .ValidateAsync(Arg.Any<Guid>(), Arg.Any<AccessRule>(), Arg.Any<IEnumerable<Guid>>(), Arg.Any<Guid?>())
            .ThrowsAsync(new BadRequestException("Name is required."));

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.CreateAsync(rule, []));
        Assert.Equal("Name is required.", ex.Message);
        await sutProvider.GetDependency<IAccessRuleRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default!);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs()
            .SetAccessRuleAssociationsAsync(default, default, default!, default!);
    }

    // The rule id is assigned by the repository, so only the outcome can name it; the attempt names the rule by name.
    [Theory, BitAutoData]
    public async Task CreateAsync_EmitsAttemptThenOutcome_WithTheRuleNameAndEditorAsActor(AccessRule rule, Guid editorId)
    {
        var sutProvider = SetupSutProvider();
        rule.Name = "Production database";
        rule.LastEditedBy = editorId;
        SetupValidator(sutProvider, rule.OrganizationId, []);
        sutProvider.GetDependency<IAccessRuleRepository>().CreateAsync(rule).Returns(rule);

        await sutProvider.Sut.CreateAsync(rule, []);

        var emitter = sutProvider.GetDependency<IAccessAuditEventEmitter>();
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.RuleCreated && e.Phase == AccessAuditEventPhase.Attempt
            && e.OrganizationId == rule.OrganizationId && e.ActorId == editorId
            && e.RuleName == "Production database" && e.AccessRuleId == null
            && e.OccurredAt == _now));
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.RuleCreated && e.Phase == AccessAuditEventPhase.Outcome
            && e.AccessRuleId == rule.Id && e.RuleName == "Production database"));
    }

    // The outcome waits for the collection links, so a failure there leaves the create in doubt rather than recorded
    // as clean.
    [Theory, BitAutoData]
    public async Task CreateAsync_CollectionLinkWriteFails_EmitsAttemptButNoOutcome(AccessRule rule)
    {
        var sutProvider = SetupSutProvider();
        SetupValidator(sutProvider, rule.OrganizationId, []);
        sutProvider.GetDependency<IAccessRuleRepository>().CreateAsync(rule).Returns(rule);
        sutProvider.GetDependency<ICollectionRepository>()
            .SetAccessRuleAssociationsAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<IEnumerable<Guid>>(),
                Arg.Any<IEnumerable<Guid>>())
            .ThrowsAsync(new InvalidOperationException("boom"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => sutProvider.Sut.CreateAsync(rule, []));

        var emitter = sutProvider.GetDependency<IAccessAuditEventEmitter>();
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Phase == AccessAuditEventPhase.Attempt));
        await emitter.DidNotReceive().EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Phase == AccessAuditEventPhase.Outcome));
    }

    private static void SetupValidator(SutProvider<CreateAccessRuleCommand> sutProvider, Guid organizationId,
        List<Guid> validatedCollectionIds)
        => sutProvider.GetDependency<IAccessRuleWriteValidator>()
            .ValidateAsync(organizationId, Arg.Any<AccessRule>(), Arg.Any<IEnumerable<Guid>>(), Arg.Any<Guid?>())
            .Returns(validatedCollectionIds);

    private static SutProvider<CreateAccessRuleCommand> SetupSutProvider()
    {
        var sutProvider = new SutProvider<CreateAccessRuleCommand>()
            .WithFakeTimeProvider()
            .Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }
}
