using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Bit.Infrastructure.IntegrationTest.AdminConsole;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Pam.Repositories;

/// <summary>
/// Concurrent submits and activations for one requester and item must leave at most one open request and one live
/// lease (PAM-223).
/// </summary>
public class AccessRequestSubmitRaceTests
{
    private const int Contenders = 8;

    private const string SqliteSkipReason =
        "SQLite serializes every writer at the database level, so this race cannot occur there.";

    [DatabaseTheory, DatabaseData]
    public async Task CreateIfNoOpenAsync_ConcurrentSubmits_CreateExactlyOneOpenRequest(
        Database database,
        IOrganizationRepository organizationRepository,
        IAccessRequestRepository accessRequestRepository)
    {
        Assert.SkipWhen(database.Type == SupportedDatabaseProviders.Sqlite, SqliteSkipReason);

        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var requesterId = Guid.NewGuid();
        var cipherId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var outcomes = await Task.WhenAll(Enumerable.Range(0, Contenders).Select(_ => Task.Run(() =>
            accessRequestRepository.CreateIfNoOpenAsync(BuildOpenRequest(organization.Id, requesterId, cipherId, now), now))));

        Assert.Equal(1, outcomes.Count(o => o == AccessRequestCreateOutcome.Created));
        Assert.Equal(Contenders - 1, outcomes.Count(o => o == AccessRequestCreateOutcome.PendingRequestExists));
        var stored = await accessRequestRepository.GetManyByRequesterIdAsync(requesterId, null, now);
        Assert.Single(stored, r => r.CipherId == cipherId);
    }

    [DatabaseTheory, DatabaseData]
    public async Task CreateAutoApprovedAsync_ConcurrentSubmits_CreateExactlyOneApprovedRequest(
        Database database,
        IOrganizationRepository organizationRepository,
        IAccessRequestRepository accessRequestRepository)
    {
        Assert.SkipWhen(database.Type == SupportedDatabaseProviders.Sqlite, SqliteSkipReason);

        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var requesterId = Guid.NewGuid();
        var cipherId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var outcomes = await Task.WhenAll(Enumerable.Range(0, Contenders).Select(_ => Task.Run(() =>
        {
            var request = BuildOpenRequest(organization.Id, requesterId, cipherId, now);
            request.Action = AccessRequestAction.Approved;
            request.ActionDate = now;
            var decision = new AccessDecision
            {
                Id = CombGuid.Generate(),
                AccessRequestId = request.Id,
                DeciderKind = AccessDeciderKind.Automatic,
                Verdict = AccessDecisionVerdict.Approve,
                CreationDate = now,
            };
            return accessRequestRepository.CreateAutoApprovedAsync(request, decision);
        })));

        Assert.Equal(1, outcomes.Count(o => o == AccessRequestCreateOutcome.Created));
        Assert.Equal(Contenders - 1, outcomes.Count(o => o == AccessRequestCreateOutcome.ApprovedRequestExists));
    }

    [DatabaseTheory, DatabaseData]
    public async Task CreateIfNoOpenAsync_WhileALeaseIsLive_ReportsActiveLease(
        IOrganizationRepository organizationRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var requesterId = Guid.NewGuid();
        var cipherId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var approved = await CreateApprovedAsync(accessRequestRepository, organization.Id, requesterId, cipherId, now);
        Assert.Equal(AccessLeaseMintOutcome.Minted,
            await accessLeaseRepository.CreateFromApprovedRequestAsync(BuildLeaseFor(approved, now), now, false));

        var outcome = await accessRequestRepository.CreateIfNoOpenAsync(
            BuildOpenRequest(organization.Id, requesterId, cipherId, now), now);

        Assert.Equal(AccessRequestCreateOutcome.ActiveLeaseExists, outcome);
    }

    [DatabaseTheory, DatabaseData]
    public async Task CreateFromApprovedRequestAsync_ConcurrentActivationsOfTwoRequests_MintExactlyOneLease(
        Database database,
        IOrganizationRepository organizationRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        Assert.SkipWhen(database.Type == SupportedDatabaseProviders.Sqlite, SqliteSkipReason);

        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var requesterId = Guid.NewGuid();
        var cipherId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var first = await CreateApprovedAsync(accessRequestRepository, organization.Id, requesterId, cipherId, now);
        var second = await CreateApprovedAsync(accessRequestRepository, organization.Id, requesterId, cipherId, now);

        // enforceSingleActiveLease is false: the guard must hold on rules without single active lease too.
        var outcomes = await Task.WhenAll(
            Task.Run(() => accessLeaseRepository.CreateFromApprovedRequestAsync(BuildLeaseFor(first, now), now, false)),
            Task.Run(() => accessLeaseRepository.CreateFromApprovedRequestAsync(BuildLeaseFor(second, now), now, false)));

        Assert.Equal(1, outcomes.Count(o => o == AccessLeaseMintOutcome.Minted));
        Assert.Equal(1, outcomes.Count(o => o == AccessLeaseMintOutcome.RequesterActiveLeaseConflict));
        Assert.Single(await accessLeaseRepository.GetManyActiveByRequesterIdAsync(requesterId, now));
    }

    private static AccessRequest BuildOpenRequest(Guid organizationId, Guid requesterId, Guid cipherId, DateTime now)
        => new()
        {
            Id = CombGuid.Generate(),
            OrganizationId = organizationId,
            CollectionId = Guid.NewGuid(),
            CipherId = cipherId,
            RequesterId = requesterId,
            NotBefore = now,
            NotAfter = now.AddHours(1),
            Reason = "audit",
            CreationDate = now,
        };

    private static async Task<AccessRequest> CreateApprovedAsync(IAccessRequestRepository accessRequestRepository,
        Guid organizationId, Guid requesterId, Guid cipherId, DateTime now)
    {
        var request = BuildOpenRequest(organizationId, requesterId, cipherId, now);
        request.NotBefore = now.AddMinutes(-1);
        request.Action = AccessRequestAction.Approved;
        request.ActionDate = now;
        return await accessRequestRepository.CreateAsync(request);
    }

    private static AccessLease BuildLeaseFor(AccessRequest request, DateTime now)
        => new()
        {
            Id = CombGuid.Generate(),
            AccessRequestId = request.Id,
            OrganizationId = request.OrganizationId,
            CollectionId = request.CollectionId,
            CipherId = request.CipherId,
            RequesterId = request.RequesterId,
            Action = AccessLeaseAction.None,
            NotBefore = now,
            NotAfter = request.NotAfter,
            CreationDate = now,
        };
}
