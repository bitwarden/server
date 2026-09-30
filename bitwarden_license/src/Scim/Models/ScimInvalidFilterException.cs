using Bit.Core.Exceptions;

namespace Bit.Scim.Models;

/// <summary>
/// Thrown when a SCIM filter expression uses an unsupported operator or attribute.
/// Per RFC 7644 §3.4.2.2, the server responds with a 400 Bad Request and a
/// <c>scimType</c> of <c>invalidFilter</c>.
/// </summary>
public class ScimInvalidFilterException : BadRequestException
{
    public ScimInvalidFilterException(string message)
        : base(message)
    { }
}
