using System;
using Bit.Core.Models.Api;

namespace Bit.Api.Models.Response
{
    public class OrganizationSponsorshipSyncStatusResponseModel : ResponseModel
    {
        public OrganizationSponsorshipSyncStatusResponseModel(DateTime? lastSyncDate)
            : base("syncStatus")
        {
            LastSyncDate = lastSyncDate;
        }

        public DateTime? LastSyncDate { get; set; }
    }
}
