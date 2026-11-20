using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.CurrentUser;
using Bit.OrganizationKeys.Commands;
using Bit.OrganizationKeys.Models.Requests;

namespace Bit.OrganizationKeys.Handlers;

internal sealed class SetOrganizationKeyIdHandler(
    ICurrentUserContext currentUserContext,
    IOrganizationUserRepository organizationUserRepository,
    ISetOrganizationKeyIdCommand setOrganizationKeyIdCommand)
{
    /// <remarks>
    /// The endpoint's <c>RequestValidationEndpointFilter</c> validates <paramref name="request"/> before this runs.
    /// </remarks>
    public async Task HandleAsync(Guid organizationId, SetOrganizationKeyIdRequest request)
    {
        // Only a member who holds the organization key can know its key id.
        var organizationUser =
            await organizationUserRepository.GetByOrganizationAsync(organizationId, currentUserContext.UserId);
        if (string.IsNullOrWhiteSpace(organizationUser?.Key))
        {
            throw new BadRequestException("Organization key is not available.");
        }

        await setOrganizationKeyIdCommand.SetOrganizationKeyIdAsync(organizationId, request.ToKeyId());
    }
}
