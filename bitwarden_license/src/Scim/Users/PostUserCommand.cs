#nullable enable

using Bit.Core;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Errors;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.AdminConsole.Utilities.Commands;
using Bit.Core.Billing.Pricing;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Scim.Context;
using Bit.Scim.Models;
using Bit.Scim.Users.Interfaces;
using static Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Errors.ErrorMapper;

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
    IPricingClient pricingClient)
    : IPostUserCommand
{
    public async Task<OrganizationUserUserDetails?> PostUserAsync(Guid organizationId, ScimUserRequestModel model)
    {
        if (featureService.IsEnabled(FeatureFlagKeys.ScimInviteUserOptimization) is false)
        {
            return await InviteScimOrganizationUserAsync(model, organizationId, scimContext.RequestScimProvider);
        }

        return await InviteScimOrganizationUserAsync_vNext(model, organizationId, scimContext.RequestScimProvider);
    }

    private async Task<OrganizationUserUserDetails?> InviteScimOrganizationUserAsync_vNext(
        ScimUserRequestModel model,
        Guid organizationId,
        ScimProviderType scimProvider)
    {
        var organization = await organizationRepository.GetByIdAsync(organizationId);

        if (organization is null)
        {
            throw new NotFoundException();
        }

        var plan = await pricingClient.GetPlanOrThrow(organization.PlanType);

        var request = model.ToRequest(
            scimProvider: scimProvider,
            inviteOrganization: new InviteOrganization(organization, plan),
            performedAt: timeProvider.GetUtcNow());

        var orgUsers = await organizationUserRepository
            .GetManyDetailsByOrganizationAsync(request.InviteOrganization.OrganizationId);

        if (orgUsers.Any(existingUser =>
                request.Invites.First().Email.Equals(existingUser.Email, StringComparison.OrdinalIgnoreCase) ||
                request.Invites.First().ExternalId.Equals(existingUser.ExternalId, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ConflictException("User already exists.");
        }

        var result = await inviteOrganizationUsersCommand.InviteScimOrganizationUserAsync(request);

        var invitedOrganizationUserId = result switch
        {
            Success<ScimInviteOrganizationUsersResponse> success => success.Value.InvitedUser.Id,
            Failure<ScimInviteOrganizationUsersResponse> { Error.Message: NoUsersToInviteError.Code } => (Guid?)null,
            Failure<ScimInviteOrganizationUsersResponse> failure => throw MapToBitException(failure.Error),
            _ => throw new InvalidOperationException()
        };

        var organizationUser = invitedOrganizationUserId.HasValue
            ? await organizationUserRepository.GetDetailsByIdAsync(invitedOrganizationUserId.Value)
            : null;

        return organizationUser;
    }

    private async Task<OrganizationUserUserDetails?> InviteScimOrganizationUserAsync(
        ScimUserRequestModel model,
        Guid organizationId,
        ScimProviderType scimProvider)
    {
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

        var invitedOrgUser = await organizationService.InviteUserAsync(organizationId, invitingUserId: null,
            EventSystemUser.SCIM,
            invite,
            externalId);
        var orgUser = await organizationUserRepository.GetDetailsByIdAsync(invitedOrgUser.Id);

        return orgUser;
    }
}
