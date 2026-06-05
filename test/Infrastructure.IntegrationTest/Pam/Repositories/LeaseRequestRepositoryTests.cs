using Bit.Core.Pam.Entities;
using Bit.Core.Pam.Enums;
using Bit.Core.Pam.Repositories;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Bit.Infrastructure.IntegrationTest.AdminConsole;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Pam.Repositories;

public class LeaseRequestRepositoryTests
{
    [DatabaseTheory, DatabaseData]
    public async Task GetManyInboxPendingByCollectionIdsAsync_ReturnsPendingWithDenormalizedFields(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        ILeaseRequestRepository leaseRequestRepository)
    {
        var requester = await userRepository.CreateTestUserAsync("requester");
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;

        var pending = await leaseRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, requester.Id, LeaseRequestStatus.Pending, now));
        // A resolved request on the same collection must NOT appear in the pending inbox.
        await leaseRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, requester.Id, LeaseRequestStatus.Denied, now));

        var pendingRows = await leaseRequestRepository.GetManyInboxPendingByCollectionIdsAsync([collection.Id]);

        var row = Assert.Single(pendingRows);
        Assert.Equal(pending.Id, row.Id);
        Assert.Equal(LeaseRequestStatus.Pending, row.Status);
        Assert.Equal(collection.Name, row.CollectionName);
        Assert.Equal(requester.Email, row.RequesterEmail);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyInboxPendingByCollectionIdsAsync_OtherCollection_NotReturned(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        ILeaseRequestRepository leaseRequestRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;
        await leaseRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), LeaseRequestStatus.Pending, now));

        var rows = await leaseRequestRepository.GetManyInboxPendingByCollectionIdsAsync([Guid.NewGuid()]);

        Assert.Empty(rows);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyInboxHistoryByCollectionIdsAsync_RespectsStatusAndWindow(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        ILeaseRequestRepository leaseRequestRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;

        var resolved = await leaseRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), LeaseRequestStatus.Approved, now));
        // Pending requests are excluded from history.
        await leaseRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), LeaseRequestStatus.Pending, now));
        // A resolved request older than the window is excluded.
        await leaseRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), LeaseRequestStatus.Denied, now.AddDays(-120)));

        var history = await leaseRequestRepository.GetManyInboxHistoryByCollectionIdsAsync(
            [collection.Id], now.AddDays(-90));

        var row = Assert.Single(history);
        Assert.Equal(resolved.Id, row.Id);
    }

    [DatabaseTheory, DatabaseData]
    public async Task ResolveWithDecisionAsync_ResolvesRequestAndRecordsHumanDecision(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        ILeaseRequestRepository leaseRequestRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;
        var approverId = Guid.NewGuid();

        var request = await leaseRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), LeaseRequestStatus.Pending, now));

        var decision = new LeaseDecision
        {
            Id = CoreHelpers.GenerateComb(),
            LeaseRequestId = request.Id,
            DeciderKind = LeaseDecisionKind.Human,
            ApproverId = approverId,
            Decision = LeaseDecisionVerdict.Approve,
            Comment = "approved for audit",
            CreationDate = now,
        };

        await leaseRequestRepository.ResolveWithDecisionAsync(request, decision, LeaseRequestStatus.Approved, now);

        var persisted = await leaseRequestRepository.GetByIdAsync(request.Id);
        Assert.NotNull(persisted);
        Assert.Equal(LeaseRequestStatus.Approved, persisted!.Status);
        Assert.NotNull(persisted.ResolvedDate);

        // The human decision surfaces as the resolver in the inbox projection.
        var history = await leaseRequestRepository.GetManyInboxHistoryByCollectionIdsAsync(
            [collection.Id], now.AddDays(-1));
        var row = Assert.Single(history);
        Assert.Equal(approverId, row.ResolverId);
        Assert.Equal("approved for audit", row.ResolverComment);
    }

    private static LeaseRequest BuildRequest(
        Guid organizationId, Guid collectionId, Guid requesterId, LeaseRequestStatus status, DateTime creationDate)
        => new()
        {
            OrganizationId = organizationId,
            CollectionId = collectionId,
            CipherId = Guid.NewGuid(),
            RequesterId = requesterId,
            NotBefore = creationDate.AddHours(1),
            NotAfter = creationDate.AddHours(2),
            Reason = "audit",
            Status = status,
            CreationDate = creationDate,
            ResolvedDate = status == LeaseRequestStatus.Pending ? null : creationDate,
        };
}
