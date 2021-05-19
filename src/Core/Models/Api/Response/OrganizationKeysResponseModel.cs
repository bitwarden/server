using System;
using Bit.Core.Models.Table;

namespace Bit.Core.Models.Api
{
    public class OrganizationKeysResponseModel : ResponseModel
    {
        public OrganizationKeysResponseModel(Organization org) : base("organizationKeys")
        {
            if (org == null)
            {
                throw new ArgumentNullException(nameof(org));
            }
            
            PublicKey = org.PublicKey;
            PrivateKey = org.PrivateKey;
        }
        
        public string PublicKey { get; set; }
        public string PrivateKey { get; set; }
    }
}
