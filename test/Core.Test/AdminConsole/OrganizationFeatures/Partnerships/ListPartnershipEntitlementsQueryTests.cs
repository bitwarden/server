using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Partnerships;

[SutProviderCustomize]
public class ListPartnershipEntitlementsQueryTests
{
    [Theory, BitAutoData]
    public async Task ListAsync_ShortPage_ReturnsAllWithoutToken(
        Guid organizationPartnershipId, List<OrganizationPartnershipEntitlement> entitlements,
        SutProvider<ListPartnershipEntitlementsQuery> sutProvider)
    {
        var request = new ListPartnershipEntitlementsRequest { OrganizationPartnershipId = organizationPartnershipId };
        ArrangePage(sutProvider, entitlements);

        var page = (await sutProvider.Sut.ListAsync(request)).AsSuccess;

        Assert.Equal(entitlements, page.Entitlements);
        Assert.Null(page.ContinuationToken);
    }

    [Theory, BitAutoData]
    public async Task ListAsync_MoreThanAPage_TrimsAndIssuesTokenFromLastReturnedRow(
        Guid organizationPartnershipId, SutProvider<ListPartnershipEntitlementsQuery> sutProvider)
    {
        var request = new ListPartnershipEntitlementsRequest
        {
            OrganizationPartnershipId = organizationPartnershipId,
            State = PartnershipEntitlementState.Active,
        };
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var entitlements = Enumerable.Range(0, ListPartnershipEntitlementsQuery.PageSize + 1)
            .Select(i => new OrganizationPartnershipEntitlement
            {
                Id = Guid.NewGuid(),
                ExternalId = $"customer-{i}",
                ExternalIdHash = string.Empty,
                RevisionDate = start.AddSeconds(i),
            })
            .ToList();
        ArrangePage(sutProvider, entitlements);
        var lastReturned = entitlements[ListPartnershipEntitlementsQuery.PageSize - 1];
        sutProvider.GetDependency<IPartnershipEntitlementContinuationTokenProtector>()
            .Protect(request, new PartnershipEntitlementKeyset(lastReturned.RevisionDate, lastReturned.Id))
            .Returns("next");

        var page = (await sutProvider.Sut.ListAsync(request)).AsSuccess;

        Assert.Equal(ListPartnershipEntitlementsQuery.PageSize, page.Entitlements.Count);
        Assert.Same(lastReturned, page.Entitlements[^1]);
        Assert.Equal("next", page.ContinuationToken);
    }

    [Theory, BitAutoData]
    public async Task ListAsync_ValidToken_ReadsAfterItsKeyset(
        Guid organizationPartnershipId, PartnershipEntitlementKeyset keyset,
        SutProvider<ListPartnershipEntitlementsQuery> sutProvider)
    {
        var revisedAfter = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var request = new ListPartnershipEntitlementsRequest
        {
            OrganizationPartnershipId = organizationPartnershipId,
            State = PartnershipEntitlementState.Canceled,
            RevisedAfter = revisedAfter,
            ContinuationToken = "token",
        };
        sutProvider.GetDependency<IPartnershipEntitlementContinuationTokenProtector>()
            .TryUnprotect(request, out Arg.Any<PartnershipEntitlementKeyset>())
            .Returns(call =>
            {
                call[1] = keyset;
                return true;
            });
        ArrangePage(sutProvider, []);

        await sutProvider.Sut.ListAsync(request);

        await sutProvider.GetDependency<IOrganizationPartnershipEntitlementRepository>().Received(1)
            .GetPageByRevisionDateAsync(organizationPartnershipId, PartnershipEntitlementState.Canceled, revisedAfter,
                keyset, ListPartnershipEntitlementsQuery.PageSize + 1);
    }

    [Theory, BitAutoData]
    public async Task ListAsync_RejectedToken_ReturnsInvalidContinuationTokenWithoutReading(
        Guid organizationPartnershipId, SutProvider<ListPartnershipEntitlementsQuery> sutProvider)
    {
        var request = new ListPartnershipEntitlementsRequest
        {
            OrganizationPartnershipId = organizationPartnershipId,
            ContinuationToken = "token",
        };

        var result = await sutProvider.Sut.ListAsync(request);

        Assert.Equal("invalid_continuation_token", Assert.IsType<InvalidContinuationToken>(result.AsError).Code);
        await sutProvider.GetDependency<IOrganizationPartnershipEntitlementRepository>().DidNotReceiveWithAnyArgs()
            .GetPageByRevisionDateAsync(default, default, default, default, default);
    }

    private static void ArrangePage(
        SutProvider<ListPartnershipEntitlementsQuery> sutProvider, List<OrganizationPartnershipEntitlement> rows) =>
        sutProvider.GetDependency<IOrganizationPartnershipEntitlementRepository>()
            .GetPageByRevisionDateAsync(default, default, default, default, default)
            .ReturnsForAnyArgs(rows);
}
