using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;
using GlobalSettings = Bit.Core.Settings.GlobalSettings;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Partnerships;

public class ProvisionPartnershipEntitlementCommandTests
{
    private static readonly DateTime _now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ProvisionAsync_NewExternalId_CreatesProvisionedEntitlement()
    {
        var sutProvider = CreateSutProvider();
        var partnership = ArrangePartnership(sutProvider);
        var metadata = new Dictionary<string, string> { ["plan"] = "gold" };
        var effectiveAt = _now.AddMinutes(-1);

        var result = await sutProvider.Sut.ProvisionAsync(
            CreateRequest(partnership) with { Metadata = metadata, EffectiveAt = effectiveAt });

        Assert.True(result.AsSuccess.Created);
        Assert.True(result.AsSuccess.Applied);
        var entitlement = result.AsSuccess.Entitlement;
        Assert.NotEqual(Guid.Empty, entitlement.Id);
        Assert.Equal(PartnershipEntitlementState.Provisioned, entitlement.State);
        Assert.Equal(OrganizationPartnershipEntitlement.ComputeExternalIdHash(partnership.Id, "customer-1"), entitlement.ExternalIdHash);
        Assert.Equal(effectiveAt, entitlement.LastAppliedEffectiveDate);
        Assert.Equal(metadata, entitlement.GetMetadata());
        await sutProvider.GetDependency<IOrganizationPartnershipEntitlementRepository>().Received(1).CreateAsync(entitlement);
    }

    [Fact]
    public async Task ProvisionAsync_NewExternalId_LogsProvisionedEvent()
    {
        var sutProvider = CreateSutProvider();
        var partnership = ArrangePartnership(sutProvider);

        await sutProvider.Sut.ProvisionAsync(CreateRequest(partnership));

        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogOrganizationPartnershipEventAsync(partnership.OrganizationId, EventType.PartnershipEntitlement_Provisioned, _now);
    }

    [Theory]
    [InlineData(PartnershipEntitlementState.Provisioned)]
    [InlineData(PartnershipEntitlementState.Active)]
    [InlineData(PartnershipEntitlementState.Suspended)]
    public async Task ProvisionAsync_ExistingNotCanceled_ReturnsExistingUnchanged(PartnershipEntitlementState state)
    {
        var sutProvider = CreateSutProvider();
        var partnership = ArrangePartnership(sutProvider);
        var existing = ArrangeExisting(sutProvider, partnership, state);

        var result = await sutProvider.Sut.ProvisionAsync(CreateRequest(partnership));

        Assert.Same(existing, result.AsSuccess.Entitlement);
        Assert.False(result.AsSuccess.Created);
        Assert.False(result.AsSuccess.Applied);
        Assert.Equal(state, existing.State);
        await sutProvider.GetDependency<IOrganizationPartnershipEntitlementRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
        await sutProvider.GetDependency<IOrganizationPartnershipEntitlementRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Fact]
    public async Task ProvisionAsync_ExistingCanceled_ClearsBindingAndReturnsToProvisioned()
    {
        var sutProvider = CreateSutProvider();
        var partnership = ArrangePartnership(sutProvider);
        var existing = ArrangeExisting(sutProvider, partnership, PartnershipEntitlementState.Canceled);

        var result = await sutProvider.Sut.ProvisionAsync(CreateRequest(partnership));

        var reprovisioned = result.AsSuccess.Entitlement;
        Assert.Same(existing, reprovisioned);
        Assert.False(result.AsSuccess.Created);
        Assert.True(result.AsSuccess.Applied);
        Assert.Equal(PartnershipEntitlementState.Provisioned, reprovisioned.State);
        Assert.Null(reprovisioned.UserId);
        Assert.Null(reprovisioned.AccountRef);
        Assert.Null(reprovisioned.BoundDate);
        Assert.Null(reprovisioned.SuspendedDate);
        Assert.Null(reprovisioned.CanceledDate);
        Assert.Null(reprovisioned.ResumeWindowExpirationDate);
        await sutProvider.GetDependency<IOrganizationPartnershipEntitlementRepository>().Received(1).ReplaceAsync(existing);
    }

    [Fact]
    public async Task ProvisionAsync_ReprovisionAfterCancel_LetsDifferentUserActivateWithNewAccountRef()
    {
        var provisionSut = CreateSutProvider();
        var partnership = ArrangePartnership(provisionSut);
        var entitlement = ArrangeExisting(provisionSut, partnership, PartnershipEntitlementState.Canceled);
        var originalAccountRef = entitlement.AccountRef;
        var transitionSut = new SutProvider<TransitionPartnershipEntitlementCommand>()
            .SetDependency(new GlobalSettings())
            .WithFakeTimeProvider()
            .Create();
        transitionSut.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        transitionSut.GetDependency<IOrganizationPartnershipRepository>().GetByIdAsync(partnership.Id).Returns(partnership);
        transitionSut.GetDependency<IOrganizationPartnershipEntitlementRepository>()
            .GetByExternalIdAsync(partnership.Id, entitlement.ExternalId).Returns(entitlement);
        var newUserId = Guid.NewGuid();

        await provisionSut.Sut.ProvisionAsync(CreateRequest(partnership));
        var result = await transitionSut.Sut.TransitionAsync(new TransitionPartnershipEntitlementRequest
        {
            OrganizationPartnershipId = partnership.Id,
            ExternalId = entitlement.ExternalId,
            Action = PartnershipEntitlementAction.Activate,
            UserId = newUserId,
        });

        var activated = result.AsSuccess.Entitlement;
        Assert.Equal(PartnershipEntitlementState.Active, activated.State);
        Assert.Equal(newUserId, activated.UserId);
        Assert.NotNull(activated.AccountRef);
        Assert.NotEqual(originalAccountRef, activated.AccountRef);
    }

    [Fact]
    public async Task ProvisionAsync_ReprovisionWithStaleEffectiveAt_ReturnsNotApplied()
    {
        var sutProvider = CreateSutProvider();
        var partnership = ArrangePartnership(sutProvider);
        var existing = ArrangeExisting(sutProvider, partnership, PartnershipEntitlementState.Canceled);
        var lastApplied = existing.LastAppliedEffectiveDate;

        var result = await sutProvider.Sut.ProvisionAsync(
            CreateRequest(partnership) with { EffectiveAt = lastApplied.AddSeconds(-1) });

        Assert.False(result.AsSuccess.Applied);
        Assert.Equal(PartnershipEntitlementAppliedReasons.StaleTransition, result.AsSuccess.AppliedReason);
        Assert.Equal(lastApplied, result.AsSuccess.LastAppliedAt);
        Assert.Equal(PartnershipEntitlementState.Canceled, existing.State);
        await sutProvider.GetDependency<IOrganizationPartnershipEntitlementRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Fact]
    public async Task ProvisionAsync_EffectiveAtInFuture_ReturnsError()
    {
        var sutProvider = CreateSutProvider();
        var partnership = ArrangePartnership(sutProvider);

        var result = await sutProvider.Sut.ProvisionAsync(CreateRequest(partnership) with { EffectiveAt = _now.AddSeconds(1) });

        Assert.Equal("effective_at_in_future", Assert.IsType<EffectiveAtInFuture>(result.AsError).Code);
    }

    [Fact]
    public async Task ProvisionAsync_PartnershipNotFound_ReturnsError()
    {
        var sutProvider = CreateSutProvider();

        var result = await sutProvider.Sut.ProvisionAsync(new ProvisionPartnershipEntitlementRequest
        {
            OrganizationPartnershipId = Guid.NewGuid(),
            ExternalId = "customer-1",
        });

        Assert.Equal("not_found", Assert.IsType<PartnershipNotFound>(result.AsError).Code);
    }

    [Fact]
    public async Task ProvisionAsync_PartnershipNotActive_ReturnsError()
    {
        var sutProvider = CreateSutProvider();
        var partnership = ArrangePartnership(sutProvider, PartnershipStatus.Inactive);

        var result = await sutProvider.Sut.ProvisionAsync(CreateRequest(partnership));

        Assert.Equal("partnership_not_active", Assert.IsType<PartnershipNotActive>(result.AsError).Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ProvisionAsync_BlankExternalId_ReturnsValidationError(string externalId)
    {
        var sutProvider = CreateSutProvider();
        var partnership = ArrangePartnership(sutProvider);

        var result = await sutProvider.Sut.ProvisionAsync(CreateRequest(partnership) with { ExternalId = externalId });

        Assert.Equal("invalid_external_id", Assert.IsType<InvalidExternalId>(result.AsError).Code);
    }

    [Fact]
    public async Task ProvisionAsync_ExternalIdOverMaxLength_ReturnsValidationError()
    {
        var sutProvider = CreateSutProvider();
        var partnership = ArrangePartnership(sutProvider);
        var externalId = new string('a', OrganizationPartnershipEntitlement.ExternalIdMaxLength + 1);

        var result = await sutProvider.Sut.ProvisionAsync(CreateRequest(partnership) with { ExternalId = externalId });

        Assert.IsType<InvalidExternalId>(result.AsError);
    }

    [Fact]
    public async Task ProvisionAsync_ExternalIdWithProtectedPrefix_ReturnsValidationError()
    {
        var sutProvider = CreateSutProvider();
        var partnership = ArrangePartnership(sutProvider);

        var result = await sutProvider.Sut.ProvisionAsync(CreateRequest(partnership) with { ExternalId = "P|cust_8827341" });

        Assert.IsType<InvalidExternalId>(result.AsError);
    }

    [Fact]
    public async Task ProvisionAsync_ExternalIdAtMaxLength_IsAccepted()
    {
        var sutProvider = CreateSutProvider();
        var partnership = ArrangePartnership(sutProvider);
        var externalId = new string('a', OrganizationPartnershipEntitlement.ExternalIdMaxLength);

        var result = await sutProvider.Sut.ProvisionAsync(CreateRequest(partnership) with { ExternalId = externalId });

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ProvisionAsync_TooManyMetadataKeys_ReturnsValidationError()
    {
        var sutProvider = CreateSutProvider();
        var partnership = ArrangePartnership(sutProvider);
        var metadata = Enumerable.Range(0, ProvisionPartnershipEntitlementRequest.MaxMetadataKeys + 1)
            .ToDictionary(i => $"key{i}", i => "value");

        var result = await sutProvider.Sut.ProvisionAsync(CreateRequest(partnership) with { Metadata = metadata });

        Assert.Equal("too_many_metadata_keys", Assert.IsType<TooManyMetadataKeys>(result.AsError).Code);
    }

    [Fact]
    public async Task ProvisionAsync_MaxMetadataKeys_IsAccepted()
    {
        var sutProvider = CreateSutProvider();
        var partnership = ArrangePartnership(sutProvider);
        var metadata = Enumerable.Range(0, ProvisionPartnershipEntitlementRequest.MaxMetadataKeys)
            .ToDictionary(i => $"key{i}", i => "value");

        var result = await sutProvider.Sut.ProvisionAsync(CreateRequest(partnership) with { Metadata = metadata });

        Assert.True(result.IsSuccess);
    }

    private static SutProvider<ProvisionPartnershipEntitlementCommand> CreateSutProvider()
    {
        var sutProvider = new SutProvider<ProvisionPartnershipEntitlementCommand>()
            .WithFakeTimeProvider()
            .Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }

    private static OrganizationPartnership ArrangePartnership(
        SutProvider<ProvisionPartnershipEntitlementCommand> sutProvider,
        PartnershipStatus status = PartnershipStatus.Active)
    {
        var partnership = new OrganizationPartnership
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            Name = "Partner",
            Status = status,
        };
        sutProvider.GetDependency<IOrganizationPartnershipRepository>()
            .GetByIdAsync(partnership.Id)
            .Returns(partnership);
        return partnership;
    }

    private static OrganizationPartnershipEntitlement ArrangeExisting(
        SutProvider<ProvisionPartnershipEntitlementCommand> sutProvider,
        OrganizationPartnership partnership,
        PartnershipEntitlementState state)
    {
        var existing = new OrganizationPartnershipEntitlement
        {
            Id = Guid.NewGuid(),
            OrganizationPartnershipId = partnership.Id,
            ExternalId = "customer-1",
            ExternalIdHash = "hash",
            State = state,
            LastAppliedEffectiveDate = _now.AddDays(-1),
        };
        if (state != PartnershipEntitlementState.Provisioned)
        {
            existing.UserId = Guid.NewGuid();
            existing.SetNewAccountRef();
            existing.BoundDate = _now.AddDays(-10);
        }
        if (state == PartnershipEntitlementState.Canceled)
        {
            existing.SuspendedDate = _now.AddDays(-3);
            existing.CanceledDate = _now.AddDays(-1);
            existing.ResumeWindowExpirationDate = _now.AddDays(29);
        }

        sutProvider.GetDependency<IOrganizationPartnershipEntitlementRepository>()
            .GetByExternalIdAsync(partnership.Id, existing.ExternalId)
            .Returns(existing);
        return existing;
    }

    private static ProvisionPartnershipEntitlementRequest CreateRequest(OrganizationPartnership partnership) =>
        new()
        {
            OrganizationPartnershipId = partnership.Id,
            ExternalId = "customer-1",
        };
}
