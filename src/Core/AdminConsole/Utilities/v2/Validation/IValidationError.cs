using System.Text.Json.Nodes;

namespace Bit.Core.AdminConsole.Utilities.v2.Validation;

/// <summary>
/// An error tied to a specific request property. Implementing this on an <see cref="Error"/> allows
/// the API layer to render an RFC 7807 validation problem response keyed by <see cref="PropertyName"/>,
/// with a stable <see cref="Type"/> code that clients can localize.
/// </summary>
public interface IValidationError
{
    string PropertyName { get; }
    string Message { get; }
    string Type { get; }

    /// <summary>
    /// The substitutions a client needs to render its own localized message for <see cref="Type"/> — the limit that
    /// was exceeded, the bound that was missed. Null when the code needs none, which is most of them. Carries the
    /// limit that was breached, never anything derived from the value that breached it.
    /// </summary>
    /// <remarks>
    /// A default member so the errors that already implement this interface compile untouched and take parameters
    /// on one at a time, rather than every one of them changing to say it has none.
    /// </remarks>
    JsonObject? Parameters => null;
}
