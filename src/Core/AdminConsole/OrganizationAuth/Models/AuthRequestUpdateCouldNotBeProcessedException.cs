namespace Bit.Core.AdminConsole.OrganizationAuth.Models;

public class AuthRequestUpdateCouldNotBeProcessedException : AuthRequestUpdateProcessingException
{
    public AuthRequestUpdateCouldNotBeProcessedException()
        : base($"An auth request could not be processed.") { }

    public AuthRequestUpdateCouldNotBeProcessedException(Guid id)
        : base($"An auth request with id {id} could not be processed.") { }
}
