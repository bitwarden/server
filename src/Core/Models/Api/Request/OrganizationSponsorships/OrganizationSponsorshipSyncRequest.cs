using System;
using System.Collections.Generic;

namespace Bit.Core.Models.Api.Request.OrganizationSponsorships
{
    public class OrganizationSponsorshipSyncRequestModel
    {
        public Guid SponsoringOrganizationCloudId { get; set; }
        public IEnumerable<Guid> AllOrganizationUserIds { get; set; }
        public IEnumerable<OrganizationSponsorshipModel> SponsorshipsBatch { get; set; }
    }
}
