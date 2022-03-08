using System;

namespace Bit.Core.Models.Api.Request.OrganizationSponsorships
{
    public class OrganizationSponsorshipModel
    {
        public Guid SponsoringOrganizationCloudId { get; set; }
        public Guid? SponsoringOrganizationUserId { get; set; }
    }
}
