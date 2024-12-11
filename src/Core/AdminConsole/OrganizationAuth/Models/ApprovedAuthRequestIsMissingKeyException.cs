namespace Bit.Core.AdminConsole.OrganizationAuth.Models;

public class ApprovedAuthRequestIsMissingKeyException : AuthRequestUpdateProcessingException
{
    public ApprovedAuthRequestIsMissingKeyException(Guid id)
        : base(
            $"An auth request with id {id} was approved, but no key was provided. This auth request can not be approved."
        ) { }
}
