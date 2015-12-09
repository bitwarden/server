using Newtonsoft.Json;

namespace Bit.Core
{
    public interface IDataObject
    {
        [JsonProperty("id")]
        string Id { get; set; }
        [JsonProperty("type")]
        string Type { get; }
    }
}
