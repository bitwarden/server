using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Bit.Core.Models.Business
{
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class ReferenceEventData
    {
        public string Id { get; set; }

        public string Layout { get; set; }

        public string Flow { get; set; }
    }
}
