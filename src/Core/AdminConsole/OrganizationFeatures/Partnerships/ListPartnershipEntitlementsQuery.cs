using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;

public class ListPartnershipEntitlementsQuery(
    IOrganizationPartnershipEntitlementRepository organizationPartnershipEntitlementRepository,
    IPartnershipEntitlementContinuationTokenProtector continuationTokenProtector)
    : IListPartnershipEntitlementsQuery
{
    public const int PageSize = 500;

    public async Task<CommandResult<PartnershipEntitlementPage>> ListAsync(ListPartnershipEntitlementsRequest request)
    {
        PartnershipEntitlementKeyset? after = null;
        if (request.ContinuationToken is not null)
        {
            if (!continuationTokenProtector.TryUnprotect(request, out var keyset))
            {
                return new InvalidContinuationToken();
            }
            after = keyset;
        }

        // One extra row tells whether another page follows without a separate count.
        var entitlements = (await organizationPartnershipEntitlementRepository.GetPageByRevisionDateAsync(
            request.OrganizationPartnershipId, request.State, request.RevisedAfter, after, PageSize + 1)).ToList();
        if (entitlements.Count <= PageSize)
        {
            return new PartnershipEntitlementPage(entitlements, null);
        }

        entitlements.RemoveRange(PageSize, entitlements.Count - PageSize);
        var last = entitlements[^1];
        var continuationToken = continuationTokenProtector.Protect(
            request, new PartnershipEntitlementKeyset(last.RevisionDate, last.Id));
        return new PartnershipEntitlementPage(entitlements, continuationToken);
    }
}
