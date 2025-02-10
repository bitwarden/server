using Bit.Core;
using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Scim.Context;
using Bit.Scim.Models;
using Bit.Scim.Users.Interfaces;

namespace Bit.Scim.Users;

public class PostUserCommand(
    IOrganizationRepository organizationRepository,
    IOrganizationUserRepository organizationUserRepository,
    IOrganizationService organizationService,
    IPaymentService paymentService,
    IScimContext scimContext,
    IFeatureService featureService,
    IInviteOrganizationUsersCommand inviteOrganizationUsersCommand,
    TimeProvider timeProvider)
    : IPostUserCommand
{
    public async Task<OrganizationUserUserDetails> PostUserAsync(Guid organizationId, ScimUserRequestModel model)
    {
        var scimProvider = scimContext.RequestScimProvider;
        var invite = model.ToOrganizationUserInvite(scimProvider);

        var email = invite.Emails.Single();
        var externalId = model.ExternalIdForInvite();

        if (string.IsNullOrWhiteSpace(email) || !model.Active)
        {
            throw new BadRequestException();
        }

        var existingUsers = await organizationUserRepository.GetManyDetailsByOrganizationAsync(organizationId);

        if (existingUsers.Any(ou => ou.Email?.ToLowerInvariant() == email || ou.ExternalId == externalId))
        {
            throw new ConflictException();
        }

        var organization = await organizationRepository.GetByIdAsync(organizationId);

        var hasStandaloneSecretsManager = await paymentService.HasSecretsManagerStandalone(organization);
        invite.AccessSecretsManager = hasStandaloneSecretsManager;

        if (featureService.IsEnabled(FeatureFlagKeys.ScimCreateUserRefactor))
        {
            var organizationUser = (await inviteOrganizationUsersCommand.InviteScimOrganizationUserAsync(
                InviteScimOrganizationUserRequest.Create(
                    OrganizationUserSingleEmailInvite.Create(
                        email,
                        invite.Collections,
                        externalId, invite.Type ?? OrganizationUserType.User,
                        invite.Permissions),
                    OrganizationDto.FromOrganization(organization),
                    timeProvider.GetUtcNow()))).Data;

            return await organizationUserRepository.GetDetailsByIdAsync(organizationUser.Id);
        }

        var orgUser = await organizationService.InviteUserAsync(organizationId, null, EventSystemUser.SCIM, invite,
            externalId);

        return await organizationUserRepository.GetDetailsByIdAsync(orgUser.Id);

    }
}
