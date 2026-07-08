using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Billing.Pricing;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptions.Interface;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
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
    IGlobalSettings globalSettings,
    TimeProvider timeProvider)
    : IUpdateOrganizationUserCommand
{
    public async Task<CommandResult> UpdateUserAsync(UpdateOrganizationUserRequest request)
    {
        var (organizationUser, organization) = (request.OrganizationUserToUpdate, request.Organization);

        var validationResult = await validator.ValidateAsync(request);

        if (validationResult.IsError)
        {
            return validationResult.AsError;
        }

        var collectionsToSave = request.NewCollections?.ToList() ?? [];
        var groupsToSave = request.NewGroups;

        var enablingSecretsManager = !organizationUser.AccessSecretsManager && request.NewAccessSecretsManager;

        organizationUser.Type = request.NewType;
        organizationUser.Permissions = CoreHelpers.ClassToJsonData(request.NewPermissions);
        organizationUser.AccessSecretsManager = request.NewAccessSecretsManager;

        // Only autoscale after validation passes, so we never touch Stripe for an invalid request.
        if (enablingSecretsManager)
        {
            var additionalSmSeatsRequired = await countNewSmSeatsRequiredQuery.CountNewSmSeatsRequiredAsync(organizationUser.OrganizationId, 1);
            if (additionalSmSeatsRequired > 0)
            {
                // Self-hosted instances can't autoscale their Stripe subscription; reject rather than attempt a purchase.
                if (globalSettings.SelfHosted)
                {
                    return new CannotAutoscaleSecretsManagerSeatsOnSelfHost();
                }

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
