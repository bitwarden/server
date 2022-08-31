using System.Text.Json.Serialization;
using Bit.Core.Utilities;

namespace Bit.Core.Tokens;

public abstract class ExpiringTokenable : Tokenable
{
    [JsonConverter(typeof(EpochDateTimeJsonConverter))]
    public DateTime ExpirationDate { get; set; }
    public override bool Valid => ExpirationDate > DateTime.UtcNow && TokenIsValid();

    protected abstract bool TokenIsValid();
}
