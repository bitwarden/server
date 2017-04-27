using System;
using Bit.Core.Models.Table;

namespace Bit.Core.Models.Api
{
    public class CollectionResponseModel : ResponseModel
    {
        public CollectionResponseModel(Collection collection)
            : base("collection")
        {
            if(collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            Id = collection.Id.ToString();
            OrganizationId = collection.OrganizationId.ToString();
            Name = collection.Name;
        }

        public string Id { get; set; }
        public string OrganizationId { get; set; }
        public string Name { get; set; }
    }
}
