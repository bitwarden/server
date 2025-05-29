﻿using Bit.Core.Entities;

#nullable enable

namespace Bit.Core.Repositories;

public interface IOrganizationSponsorshipRepository : IRepository<OrganizationSponsorship, Guid>
{
    Task<ICollection<Guid>?> CreateManyAsync(IEnumerable<OrganizationSponsorship> organizationSponsorships);
    Task ReplaceManyAsync(IEnumerable<OrganizationSponsorship> organizationSponsorships);
    Task UpsertManyAsync(IEnumerable<OrganizationSponsorship> organizationSponsorships);
    Task DeleteManyAsync(IEnumerable<Guid> organizationSponsorshipIds);
    Task<ICollection<OrganizationSponsorship>> GetManyBySponsoringOrganizationAsync(Guid sponsoringOrganizationId);
    Task<OrganizationSponsorship?> GetBySponsoringOrganizationUserIdAsync(Guid sponsoringOrganizationUserId, bool isAdminInitiated = false);
    Task<OrganizationSponsorship?> GetBySponsoredOrganizationIdAsync(Guid sponsoredOrganizationId);
    Task<DateTime?> GetLatestSyncDateBySponsoringOrganizationIdAsync(Guid sponsoringOrganizationId);
}
