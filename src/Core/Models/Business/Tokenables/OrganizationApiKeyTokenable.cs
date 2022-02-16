using System;
using System.Text.Json.Serialization;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Tokens;

namespace Bit.Core.Models.Business.Tokenables
{
    public class OrganizationApiKeyTokenable : Tokenable
    {
        public const string ClearTextPrefix = "BWOrgApiKey_";
        public const string DataProtectorPurpose = "OrgApiKey";

        [JsonConstructor]
        public OrganizationApiKeyTokenable() {}

        // Used on cloud side
        public OrganizationApiKeyTokenable(OrganizationApiKey apiKey)
        {
            if (Enum.IsDefined(apiKey.Type))
            {
                throw new ArgumentException($"Invalid OrganizationApiKey, Type must be a defined enum of type {typeof(OrganizationApiKeyType)}.");
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
            Key = apiKey.ApiKey;
        }

        // Used on self hosted side
        public OrganizationApiKeyTokenable(Guid organizationId, string key)
        {
            OrganizationId = organizationId;
            Key = key;
        }
        
        public string Key { get; set; }
        public Guid OrganizationId { get; set; }
        public override bool Valid => OrganizationId != Guid.Empty && !string.IsNullOrWhiteSpace(Key);
    }
}
