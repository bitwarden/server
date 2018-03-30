using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Microsoft.Azure.Documents;
using Newtonsoft.Json;
using Serilog.Events;

namespace Bit.Admin.Models
{
    public class LogModel : Resource
    {
        public long EventIdHash { get; set; }
        public LogEventLevel Level { get; set; }
        public string Message { get; set; }
        public string MessageTruncated => Message.Length > 200 ? $"{Message.Substring(0, 200)}..." : Message;
        public string MessageTemplate { get; set; }
        public Error Exception { get; set; }
        public IDictionary<string, object> Properties { get; set; }

        [JsonObject(MemberSerialization.OptIn)]
        public class Error : Exception, ISerializable
        {
            [JsonProperty(PropertyName = "error")]
            public string ErrorMessage { get; set; }

            [JsonConstructor]
            public Error() { }
        }
    }
}
