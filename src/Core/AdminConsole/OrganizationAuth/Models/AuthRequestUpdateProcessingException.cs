namespace Bit.Core.AdminConsole.OrganizationAuth.Models;

public class AuthRequestUpdateProcessingException : Exception
{
    public AuthRequestUpdateProcessingException() { }

    public AuthRequestUpdateProcessingException(string message)
        : base(message) { }
}
