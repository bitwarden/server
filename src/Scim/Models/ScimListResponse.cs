using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Bit.Scim.Models
{
    public class ScimListResponse : ScimSchemaBase
    {
        public ScimListResponse(IEnumerable<ScimResource> resources)
        {
            Resources = resources;
        }

        public override string SchemaIdentifier => Constants.Messages.ListResponse;
        [JsonProperty("totalResults", Order = 0)]
        public int TotalResults => Resources == null ? 0 : Resources.Count();
        [JsonProperty("Resources", Order = 1)]
        public IEnumerable<ScimResource> Resources { get; private set; }
        [JsonProperty("startIndex", Order = 2)]
        public int StartIndex { get; set; } = 0;
        [JsonProperty("itemsPerPage", Order = 3)]
        public int ItemsPerPage => Resources == null ? 0 : Resources.Count();
    }
}
