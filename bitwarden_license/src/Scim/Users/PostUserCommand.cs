#nullable enable

using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.Billing.Pricing;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Commands;
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
    TimeProvider timeProvider,
    IPricingClient pricingClient,
    ILogger<PostUserCommand> logger)
    : IPostUserCommand
{
    public async Task<OrganizationUserUserDetails?> PostUserAsync(Guid organizationId, ScimUserRequestModel model)
    {
        var scimProvider = scimContext.RequestScimProvider;
        var invite = model.ToOrganizationUserInvite(scimProvider);

        var email = invite.Emails.Single();
        var externalId = model.ExternalIdForInvite();

        if (string.IsNullOrWhiteSpace(email) || !model.Active)
        {
            throw new BadRequestException();
        }

        var orgUsers = await organizationUserRepository.GetManyDetailsByOrganizationAsync(organizationId);
        var orgUserByEmail = orgUsers.FirstOrDefault(ou => ou.Email?.ToLowerInvariant() == email);
        if (orgUserByEmail != null)
        {
            throw new ConflictException();
        }

        var orgUserByExternalId = orgUsers.FirstOrDefault(ou => ou.ExternalId == externalId);
        if (orgUserByExternalId != null)
        {
            throw new ConflictException();
        }

        var organization = await organizationRepository.GetByIdAsync(organizationId);

        if (organization == null)
        {
            throw new NotFoundException();
        }

        var hasStandaloneSecretsManager = await paymentService.HasSecretsManagerStandalone(organization);
        invite.AccessSecretsManager = hasStandaloneSecretsManager;

        if (featureService.IsEnabled(FeatureFlagKeys.ScimInviteUserOptimization))
        {
            return await InviteScimOrganizationUserAsync(model, organization, scimProvider, hasStandaloneSecretsManager);
        }

        var invitedOrgUser = await organizationService.InviteUserAsync(organizationId, invitingUserId: null, EventSystemUser.SCIM,
            invite, externalId);
        var orgUser = await organizationUserRepository.GetDetailsByIdAsync(invitedOrgUser.Id);

        return orgUser;
    }

    private async Task<OrganizationUserUserDetails?> InviteScimOrganizationUserAsync(ScimUserRequestModel model, Organization organization, ScimProviderType scimProvider, bool hasStandaloneSecretsManager)
    {
        var plan = await pricingClient.GetPlan(organization.PlanType);

        if (plan == null)
        {
            logger.LogError("Plan {planType} not found for organization {organizationId}", organization.PlanType, organization.Id);
            return null;
        }

        var request = model.ToRequest(
            scimProvider: scimProvider,
            hasSecretsManager: hasStandaloneSecretsManager,
            inviteOrganization: new InviteOrganization(organization, plan),
            performedAt: timeProvider.GetUtcNow());

        var result = await inviteOrganizationUsersCommand.InviteScimOrganizationUserAsync(request);

        if (result is Failure<ScimInviteOrganizationUsersResponse> failure)
        {
            if (failure.ErrorMessages.Count != 0)
            {
                throw new BadRequestException(failure.ErrorMessage);
            }

            return null;
        }

        if (result is not Success<ScimInviteOrganizationUsersResponse> successfulResponse)
        {
            return null;
        }

        var invitedUser = await organizationUserRepository.GetDetailsByIdAsync(successfulResponse.Value.InvitedUser.Id);

        return invitedUser;

    }
}
