using System;
using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;
using Bit.Core.Models.Table;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Bit.Core.Models.Api
{
    public class CollectionRequestModel
    {
        [Required]
        [EncryptedString]
        [StringLength(300)]
        public string Name { get; set; }
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
            return existingCollection;
        }
    }
}
