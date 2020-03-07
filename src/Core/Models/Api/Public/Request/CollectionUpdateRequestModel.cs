using System.Collections.Generic;
using Bit.Core.Models.Table;

namespace Bit.Core.Models.Api.Public
{
    public class CollectionUpdateRequestModel : CollectionBaseModel
    {
        /// <summary>
        /// The associated groups that this collection is assigned to.
        /// </summary>
        public IEnumerable<AssociationWithPermissionsRequestModel> Groups { get; set; }

        public Collection ToCollection(Collection existingCollection)
        {
            existingCollection.ExternalId = ExternalId;
            return existingCollection;
        }
    }
}
