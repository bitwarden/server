using System.Collections.Generic;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Api;

namespace Bit.Api.Models.Response
{
    public class OrganizationApiKeyInformation : ResponseModel
    {
        public OrganizationApiKeyInformation(OrganizationApiKey key) : base("keyInformation")
        {
            KeyType = key.Type;
        }

        public OrganizationApiKeyType KeyType { get; set; }
    }
}
