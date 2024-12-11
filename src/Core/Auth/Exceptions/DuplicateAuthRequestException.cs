using Bit.Core.Exceptions;

namespace Bit.Core.Auth.Exceptions;

public class DuplicateAuthRequestException : BadRequestException
{
    public DuplicateAuthRequestException()
        : base("An authentication request with the same device already exists.") { }
}
