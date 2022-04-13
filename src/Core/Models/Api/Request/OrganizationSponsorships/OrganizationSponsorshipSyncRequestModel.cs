﻿using System;
using System.Collections.Generic;
using System.Linq;
using Bit.Core.Models.Data;

namespace Bit.Core.Models.Api.Request.OrganizationSponsorships
{
    public class OrganizationSponsorshipSyncRequestModel
    {
        public string BillingSyncKey { get; set; }
        public Guid SponsoringOrganizationCloudId { get; set; }
        public IEnumerable<OrganizationSponsorshipRequestModel> SponsorshipsBatch { get; set; }

        public OrganizationSponsorshipSyncRequestModel() { }

        public OrganizationSponsorshipSyncRequestModel(OrganizationSponsorshipSyncData syncData)
        {
            if (syncData == null)
            {
                return;
            }
            BillingSyncKey = syncData.BillingSyncKey;
            SponsoringOrganizationCloudId = syncData.SponsoringOrganizationCloudId;
            SponsorshipsBatch = syncData.SponsorshipsBatch.Select(o => new OrganizationSponsorshipRequestModel(o));
        }

        public OrganizationSponsorshipSyncData ToOrganizationSponsorshipSync()
        {
            return new OrganizationSponsorshipSyncData()
            {
                BillingSyncKey = BillingSyncKey,
                SponsoringOrganizationCloudId = SponsoringOrganizationCloudId,
                SponsorshipsBatch = SponsorshipsBatch.Select(o => o.ToOrganizationSponsorship())
            };
        }

    }
}
