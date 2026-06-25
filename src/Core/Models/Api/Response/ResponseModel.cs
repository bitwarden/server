using Newtonsoft.Json;

namespace Bit.Core.Models.Api;

/// <summary>
/// Base class for API response models.
/// </summary>
/// <remarks>
/// Deprecated in favor of <c>Bit.HttpExtensions.ResponseModel</c>, which serializes with
/// System.Text.Json instead of Newtonsoft.Json. New response models should derive from the
/// HttpExtensions type, and existing models should migrate to it over time.
/// </remarks>
[Obsolete(
    "Use Bit.HttpExtensions.ResponseModel instead.",
    DiagnosticId = "BWA0001")]
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
