using System;
using System.Text.Json.Serialization;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Tokens;

namespace Bit.Core.Models.Business.Tokenables
{
    public class BillingSyncTokenable : Tokenable
    {
        public const string ClearTextPrefix = "BWBillingSync_";
        public const string DataProtectorPurpose = "BillingSync";

        [JsonConstructor]
        public BillingSyncTokenable() {}

        // Used on cloud side
        public BillingSyncTokenable(OrganizationApiKey apiKey)
        {
            if (apiKey.Type != OrganizationApiKeyType.BillingSync)
            {
                throw new ArgumentException($"Invalid OrganizationApiKey, Type must be {nameof(OrganizationApiKeyType.BillingSync)}",
                    nameof(apiKey));
            }

            if (apiKey.OrganizationId == default)
            {
                throw new ArgumentException($"Invalid OrganizationApiKey, {nameof(OrganizationApiKey.OrganizationId)} is required",
                    nameof(apiKey));
            }
            OrganizationId = apiKey.OrganizationId;

            if (!string.IsNullOrWhiteSpace(apiKey.ApiKey))
            {
                throw new ArgumentException($"Invalid OrganizationApiKey, Requires an {nameof(OrganizationApiKey.ApiKey)}",
                    nameof(apiKey));
            }
            BillingSyncKey = apiKey.ApiKey;
        }

        // Used on self hosted side
        public BillingSyncTokenable(Guid organizationId, string billingSyncKey)
        {
            OrganizationId = organizationId;
            BillingSyncKey = billingSyncKey;
        }
        
        public string BillingSyncKey { get; set; }
        public Guid OrganizationId { get; set; }
        public override bool Valid => OrganizationId != Guid.Empty && !string.IsNullOrWhiteSpace(BillingSyncKey);
    }
}
