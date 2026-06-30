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
}
