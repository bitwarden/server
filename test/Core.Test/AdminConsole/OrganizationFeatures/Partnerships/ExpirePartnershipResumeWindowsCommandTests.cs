using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Partnerships;

public class ExpirePartnershipResumeWindowsCommandTests
{
    private static readonly DateTime _now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ExpireAsync_ExpiredBinding_ReleasesBindingAndStaysCanceled()
    {
        var sutProvider = CreateSutProvider();
        var partnership = ArrangePartnership(sutProvider);
        var entitlement = CreateExpiredEntitlement(partnership);
        ArrangeExpired(sutProvider, entitlement);

        await sutProvider.Sut.ExpireAsync();

        await sutProvider.GetDependency<IOrganizationPartnershipEntitlementRepository>().Received(1)
            .ReplaceAsync(Arg.Is<OrganizationPartnershipEntitlement>(e =>
                e.Id == entitlement.Id &&
                e.State == PartnershipEntitlementState.Canceled &&
                e.UserId == null &&
                e.AccountRef == null &&
                e.RevisionDate == _now));
    }

    [Fact]
    public async Task ExpireAsync_ExpiredBinding_LogsResumeWindowExpiredForPartnershipOrganization()
    {
        var sutProvider = CreateSutProvider();
        var partnership = ArrangePartnership(sutProvider);
        var entitlement = CreateExpiredEntitlement(partnership);
        var expiration = entitlement.ResumeWindowExpirationDate;
        ArrangeExpired(sutProvider, entitlement);

        await sutProvider.Sut.ExpireAsync();

        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationPartnershipEventAsync(
            partnership.OrganizationId, EventType.PartnershipEntitlement_ResumeWindowExpired, expiration);
    }

    [Fact]
    public async Task ExpireAsync_ExpiredBinding_LogsOnlyEventsWithoutUserIdentity()
    {
        var sutProvider = CreateSutProvider();
        var partnership = ArrangePartnership(sutProvider);
        ArrangeExpired(sutProvider, CreateExpiredEntitlement(partnership));

        await sutProvider.Sut.ExpireAsync();

        // The partnership method carries no user identity; every other IEventService method can.
        Assert.All(sutProvider.GetDependency<IEventService>().ReceivedCalls(), call =>
            Assert.Equal(nameof(IEventService.LogOrganizationPartnershipEventAsync), call.GetMethodInfo().Name));
    }

    [Fact]
    public async Task ExpireAsync_ExpiredBinding_LeavesEntitlementUnboundForAnotherUser()
    {
        var sutProvider = CreateSutProvider();
        var partnership = ArrangePartnership(sutProvider);
        var entitlement = CreateExpiredEntitlement(partnership);
        ArrangeExpired(sutProvider, entitlement);

        await sutProvider.Sut.ExpireAsync();

        // Re-provisioning and activation by a different user require a canceled record with no held binding.
        Assert.Equal(PartnershipEntitlementState.Canceled, entitlement.State);
        Assert.Null(entitlement.UserId);
        Assert.Null(entitlement.AccountRef);
    }

    [Fact]
    public async Task ExpireAsync_ReturnsReleasedCount()
    {
        var sutProvider = CreateSutProvider();
        var partnership = ArrangePartnership(sutProvider);
        ArrangeExpired(sutProvider, CreateExpiredEntitlement(partnership), CreateExpiredEntitlement(partnership));

        var result = await sutProvider.Sut.ExpireAsync();

        Assert.Equal(2, result.AsSuccess);
    }

    [Fact]
    public async Task ExpireAsync_QueriesAsOfNow()
    {
        var sutProvider = CreateSutProvider();
        ArrangeExpired(sutProvider);

        await sutProvider.Sut.ExpireAsync();

        await sutProvider.GetDependency<IOrganizationPartnershipEntitlementRepository>().Received(1)
            .GetManyCanceledWithExpiredResumeWindowAsync(_now);
    }

    [Fact]
    public async Task ExpireAsync_NothingExpired_SavesNothingAndLogsNoEvents()
    {
        var sutProvider = CreateSutProvider();
        ArrangeExpired(sutProvider);

        var result = await sutProvider.Sut.ExpireAsync();

        Assert.Equal(0, result.AsSuccess);
        await sutProvider.GetDependency<IOrganizationPartnershipEntitlementRepository>().DidNotReceiveWithAnyArgs()
            .ReplaceAsync(default!);
        Assert.Empty(sutProvider.GetDependency<IEventService>().ReceivedCalls());
    }

    [Fact]
    public async Task ExpireAsync_MultipleEntitlementsUnderOnePartnership_LooksUpPartnershipOnce()
    {
        var sutProvider = CreateSutProvider();
        var partnership = ArrangePartnership(sutProvider);
        ArrangeExpired(sutProvider,
            CreateExpiredEntitlement(partnership),
            CreateExpiredEntitlement(partnership),
            CreateExpiredEntitlement(partnership));

        await sutProvider.Sut.ExpireAsync();

        await sutProvider.GetDependency<IOrganizationPartnershipRepository>().Received(1).GetByIdAsync(partnership.Id);
    }

    [Fact]
    public async Task ExpireAsync_OneSaveThrows_ReleasesTheRest()
    {
        var sutProvider = CreateSutProvider();
        var partnership = ArrangePartnership(sutProvider);
        var failing = CreateExpiredEntitlement(partnership);
        var first = CreateExpiredEntitlement(partnership);
        var last = CreateExpiredEntitlement(partnership);
        ArrangeExpired(sutProvider, first, failing, last);
        var repository = sutProvider.GetDependency<IOrganizationPartnershipEntitlementRepository>();
        repository.ReplaceAsync(Arg.Is<OrganizationPartnershipEntitlement>(e => e.Id == failing.Id))
            .ThrowsAsync(new InvalidOperationException());

        await sutProvider.Sut.ExpireAsync();

        await repository.Received(1).ReplaceAsync(Arg.Is<OrganizationPartnershipEntitlement>(e => e.Id == first.Id));
        await repository.Received(1).ReplaceAsync(Arg.Is<OrganizationPartnershipEntitlement>(e => e.Id == last.Id));
    }

    [Fact]
    public async Task ExpireAsync_OneSaveThrows_CountsOnlySuccesses()
    {
        var sutProvider = CreateSutProvider();
        var partnership = ArrangePartnership(sutProvider);
        var failing = CreateExpiredEntitlement(partnership);
        ArrangeExpired(sutProvider, CreateExpiredEntitlement(partnership), failing, CreateExpiredEntitlement(partnership));
        sutProvider.GetDependency<IOrganizationPartnershipEntitlementRepository>()
            .ReplaceAsync(Arg.Is<OrganizationPartnershipEntitlement>(e => e.Id == failing.Id))
            .ThrowsAsync(new InvalidOperationException());

        var result = await sutProvider.Sut.ExpireAsync();

        Assert.Equal(2, result.AsSuccess);
    }

    [Fact]
    public async Task ExpireAsync_PartnershipNotFound_DoesNotRelease()
    {
        var sutProvider = CreateSutProvider();
        var orphan = CreateExpiredEntitlement(new OrganizationPartnership { Id = Guid.NewGuid() });
        ArrangeExpired(sutProvider, orphan);

        var result = await sutProvider.Sut.ExpireAsync();

        Assert.Equal(0, result.AsSuccess);
        await sutProvider.GetDependency<IOrganizationPartnershipEntitlementRepository>().DidNotReceiveWithAnyArgs()
            .ReplaceAsync(default!);
    }

    private static SutProvider<ExpirePartnershipResumeWindowsCommand> CreateSutProvider()
    {
        var sutProvider = new SutProvider<ExpirePartnershipResumeWindowsCommand>()
            .WithFakeTimeProvider()
            .Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }

    private static OrganizationPartnership ArrangePartnership(SutProvider<ExpirePartnershipResumeWindowsCommand> sutProvider)
    {
        var partnership = new OrganizationPartnership
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            Name = "Partner",
            Status = PartnershipStatus.Active,
        };
        sutProvider.GetDependency<IOrganizationPartnershipRepository>().GetByIdAsync(partnership.Id).Returns(partnership);
        return partnership;
    }

    private static void ArrangeExpired(
        SutProvider<ExpirePartnershipResumeWindowsCommand> sutProvider,
        params OrganizationPartnershipEntitlement[] entitlements) =>
        sutProvider.GetDependency<IOrganizationPartnershipEntitlementRepository>()
            .GetManyCanceledWithExpiredResumeWindowAsync(_now)
            .Returns(entitlements.ToList());

    private static OrganizationPartnershipEntitlement CreateExpiredEntitlement(OrganizationPartnership partnership)
    {
        var entitlement = new OrganizationPartnershipEntitlement
        {
            Id = Guid.NewGuid(),
            OrganizationPartnershipId = partnership.Id,
            ExternalId = "customer-1",
            ExternalIdHash = "hash",
            State = PartnershipEntitlementState.Canceled,
            UserId = Guid.NewGuid(),
            BoundDate = _now.AddDays(-60),
            CanceledDate = _now.AddDays(-31),
            ResumeWindowExpirationDate = _now.AddDays(-1),
            LastAppliedEffectiveDate = _now.AddDays(-31),
            RevisionDate = _now.AddDays(-31),
        };
        entitlement.SetNewAccountRef();
        return entitlement;
    }
}
