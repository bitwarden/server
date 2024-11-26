using Bit.Core.AdminConsole.OrganizationFeatures.Shared;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations.OrganizationSignUpCommand;

public record SignUpOrganizationResponse_vNext(
    Guid OrganizationId,
    string DisplayName,
    Guid OrganizationUserId,
    Guid CollectionId,
    params string[] ErrorMessages) : CommandResult(ErrorMessages)
{
    public SignUpOrganizationResponse_vNext(params string[] ErrorMessages) : this(
        Guid.Empty,
        string.Empty,
        Guid.Empty,
        Guid.Empty,
        ErrorMessages)
    {
    }
}
