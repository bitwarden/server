using System;
using Bit.Core.Models.Data;

namespace Bit.Core.Models.Api
{
    public class OrganizationUserCollectionResponseModel : ResponseModel
    {
        public OrganizationUserCollectionResponseModel(CollectionUserCollectionDetails details,
            string obj = "organizationUserCollection")
            : base(obj)
        {
            if(details == null)
            {
                throw new ArgumentNullException(nameof(details));
            }

            Id = details.Id.ToString();
            Name = details.Name;
            ReadOnly = details.ReadOnly;
        }

        public string Id { get; set; }
        public string Name { get; set; }
        public bool ReadOnly { get; set; }
    }
}
