using System;
using Bit.Core.Models.Api;

namespace Bit.Api.Models.Response
{
    public class OrganizationSponsorshipSyncStatus : ResponseModel
    {
        public OrganizationSponsorshipSyncStatus()
            : base("syncStatus")
        {
        }

        public DateTime? LastSyncDate { get; set; }
        
    }
}
