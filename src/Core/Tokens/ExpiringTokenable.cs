using System.Text.Json.Serialization;
using Bit.Core.Utilities;

namespace Bit.Core.Tokens;

public abstract class ExpiringTokenable : Tokenable
{
    [JsonConverter(typeof(EpochDateTimeJsonConverter))]
    public DateTime ExpirationDate { get; set; }

    /// <summary>
    /// Checks if the token is still within its valid duration and if its data is valid.
    /// <para>For data validation, this property relies on the <see cref="TokenIsValid"/> method.</para>
    /// </summary>
    public override bool Valid => ExpirationDate > DateTime.UtcNow && TokenIsValid();

    /// <summary>
    /// Validates that the token data properties are correct.
    /// <para>For expiration checks, refer to the <see cref="Valid"/> property.</para>
    /// </summary>
    protected abstract bool TokenIsValid();
}
