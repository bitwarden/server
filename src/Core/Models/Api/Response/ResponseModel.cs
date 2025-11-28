using Newtonsoft.Json;

namespace Bit.Core.Models.Api;

public abstract class ResponseModel
{
    public ResponseModel(string obj)
    {
        if (string.IsNullOrWhiteSpace(obj))
        {
            throw new ArgumentNullException(nameof(obj));
        }

        Object = obj;
    }

    [JsonProperty(Order = -200)] // Always the first property
    public string Object { get; private set; }
}
