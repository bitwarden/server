using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Bit.Core.Models.Table;
using Bit.Core.Utilities;

namespace Bit.Api.Models.Request
{
    public class CollectionRequestModel
    {
        [Required]
        [EncryptedString]
        [EncryptedStringLength(1000)]
        public string Name { get; set; }
        [StringLength(300)]
        public string ExternalId { get; set; }
        public IEnumerable<SelectionReadOnlyRequestModel> Groups { get; set; }

        public Collection ToCollection(Guid orgId)
        {
            return ToCollection(new Collection
            {
                OrganizationId = orgId
            });
        }

        public Collection ToCollection(Collection existingCollection)
        {
            existingCollection.Name = Name;
            existingCollection.ExternalId = ExternalId;
            return existingCollection;
        }
    }
}
