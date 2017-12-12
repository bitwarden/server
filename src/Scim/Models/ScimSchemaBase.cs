using System.Collections.Generic;
using Newtonsoft.Json;

namespace Bit.Scim.Models
{
    public abstract class ScimSchemaBase
    {
        [JsonProperty("schemas", Order = -10)]
        public virtual ISet<string> Schemas => new HashSet<string>(new[] { SchemaIdentifier });
        [JsonIgnore]
        public abstract string SchemaIdentifier { get; }
    }
}
