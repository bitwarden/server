using System.Text.Json.Serialization;

namespace Bit.HttpExtensions;

/// <summary>
/// Base class for API response models.
/// </summary>
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

    [JsonPropertyOrder(-200)] // Always the first property
    public string Object { get; private set; }
}
