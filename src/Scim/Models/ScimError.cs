using System.Collections.Generic;
using System.Net;
using Newtonsoft.Json;

namespace Bit.Scim.Models
{
    public class ScimError
    {
        private IEnumerable<string> _schemas;

        public ScimError()
        {
            _schemas = new[] { Constants.Messages.Error };
        }

        public ScimError(HttpStatusCode status, string detail = null)
            : this()
        {
            Status = (int)status;
            Detail = detail;
        }

        [JsonProperty("schemas")]
        public IEnumerable<string> Schemas
        {
            get => _schemas;
            set { _schemas = value; }
        }
        [JsonProperty("status")]
        public int Status { get; set; }
        [JsonProperty("detail")]
        public string Detail { get; set; }
    }
}
