using System.Text.Json.Serialization;

namespace Bit.HttpExtensions;

/// <summary>
/// Base class for API response models.
/// </summary>
public abstract class ResponseModel
{
    public ResponseModel(string obj)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(obj);

        Object = obj;
    }

    [JsonPropertyOrder(-200)] // Always the first property
    public string Object { get; private set; }
}
