using System;
using System.Collections.Generic;

namespace Bit.Api.Models.Request.Organizations
{
    public class SyncOrganizationSponsorshipsRequestModel
    {
        public List<SyncOrganizationSponsorshipsUserRequestModel> Users { get; set; }
    }

    public class SyncOrganizationSponsorshipsUserRequestModel
    {
        public Guid UserId { get; set; }
    }
}
