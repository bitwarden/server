using System;
using System.Collections.Generic;

namespace Bit.Core.Models.Api.Request.OrganizationSponsorships
{
    public class OrganizationSponsorshipSyncModel
    {
        public string BillingSyncKey { get; set; }
        public Guid SponsoringOrganizationCloudId { get; set; }
        public IEnumerable<OrganizationSponsorshipModel> SponsorshipsBatch { get; set; }
    }
}
