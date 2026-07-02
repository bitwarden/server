using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Billing.Pricing;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptions.Interface;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using OneOf.Types;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.UpdateUser.v2;

public class UpdateOrganizationUserCommand(
    IOrganizationUserRepository organizationUserRepository,
    IUpdateOrganizationUserValidator validator,
    ICountNewSmSeatsRequiredQuery countNewSmSeatsRequiredQuery,
    IUpdateSecretsManagerSubscriptionCommand updateSecretsManagerSubscriptionCommand,
    IPricingClient pricingClient,
    IEventService eventService,
    TimeProvider timeProvider)
    : IUpdateOrganizationUserCommand
{
    public async Task<CommandResult> UpdateUserAsync(UpdateOrganizationUserRequest request)
    {
        var (organizationUser, organization, organizationAbility) = (request.OrganizationUser, request.Organization, request.OrganizationAbility);

        var validationResult = await validator.ValidateAsync(new UpdateOrganizationUserValidationRequest(
            organizationUser,
            request.Type,
            request.PerformedBy,
            request.PerformedByOrganizationUser,
            request.CollectionsToSave?.ToList() ?? [],
            request.GroupsToSave,
            request.CurrentAccessIds,
            organization,
            organizationAbility));

        if (validationResult.IsError)
        {
            return validationResult.AsError;
        }

        // The validator returns the collection access with default user collections filtered out.
        var collectionsToSave = validationResult.Request.CollectionsToSave;
        var groupsToSave = validationResult.Request.GroupsToSave;

        var enablingSecretsManager = !organizationUser.AccessSecretsManager && request.AccessSecretsManager;

        organizationUser.Type = request.Type;
        organizationUser.Permissions = CoreHelpers.ClassToJsonData(request.Permissions);
        organizationUser.AccessSecretsManager = request.AccessSecretsManager;

        // Only autoscale (if required) after all validation has passed so that we know it's a valid request before
        // updating Stripe.
        if (enablingSecretsManager)
        {
            var additionalSmSeatsRequired = await countNewSmSeatsRequiredQuery.CountNewSmSeatsRequiredAsync(organizationUser.OrganizationId, 1);
            if (additionalSmSeatsRequired > 0)
            {
                // TODO: https://bitwarden.atlassian.net/browse/PM-17012
                var plan = await pricingClient.GetPlanOrThrow(organization.PlanType);
                var update = new SecretsManagerSubscriptionUpdate(organization, plan, true)
                    .AdjustSeats(additionalSmSeatsRequired);
                await updateSecretsManagerSubscriptionCommand.UpdateSubscriptionAsync(update);
            }
        }

        await organizationUserRepository.ReplaceAsync(organizationUser, collectionsToSave);

        if (groupsToSave != null)
        {
            await organizationUserRepository.UpdateGroupsAsync(organizationUser.Id, groupsToSave, timeProvider.GetUtcNow().UtcDateTime);
        }

        await eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Updated);

        return new None();
    }
}
