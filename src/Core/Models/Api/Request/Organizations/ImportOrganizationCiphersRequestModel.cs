using System.Collections.Generic;

namespace Bit.Core.Models.Api
{
    public class ImportOrganizationCiphersRequestModel
    {
        public CollectionRequestModel[] Collections { get; set; }
        public LoginRequestModel[] Logins { get; set; }
        public KeyValuePair<int, int>[] CollectionRelationships { get; set; }
    }
}
