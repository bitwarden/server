using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Billing.Pricing;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptions.Interface;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using OneOf.Types;
using CommandError = Bit.Core.AdminConsole.Utilities.v2.Error;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.UpdateUser.v2;

public class UpdateOrganizationUserCommand(
    IOrganizationUserRepository organizationUserRepository,
    IUpdateOrganizationUserValidator validator,
    ICountNewSmSeatsRequiredQuery countNewSmSeatsRequiredQuery,
    IUpdateSecretsManagerSubscriptionCommand updateSecretsManagerSubscriptionCommand,
    IPricingClient pricingClient,
    IEventService eventService,
    IGlobalSettings globalSettings,
    ICollectionRepository collectionRepository,
    IPolicyRequirementQuery policyRequirementQuery,
    TimeProvider timeProvider)
    : IUpdateOrganizationUserCommand
{
    public async Task<CommandResult> UpdateUserAsync(UpdateOrganizationUserRequest request)
    {
        var validationResult = await validator.ValidateAsync(request);

        if (validationResult.IsError)
        {
            return validationResult.AsError;
        }

        var wasDemotedFromPrivilegedRole = IsDemotingFromPrivilegedRole(request);
        var enablingSecretsManager = IsEnablingSecretsManager(request);

        var organizationUser = request.OrganizationUserToUpdate;
        organizationUser.Type = request.NewType;
        organizationUser.Permissions = CoreHelpers.ClassToJsonData(request.NewPermissions);
        organizationUser.AccessSecretsManager = request.NewAccessSecretsManager;

        if (enablingSecretsManager)
        {
            var commandError = await TryEnablingSecretsManagerAsync(request);
            if (commandError is not null)
            {
                return commandError;
            }
        }

        await organizationUserRepository.ReplaceAsync(organizationUser, request.NewCollections?.ToList() ?? []);

        if (request.NewGroups != null)
        {
            await organizationUserRepository.UpdateGroupsAsync(organizationUser.Id, request.NewGroups, timeProvider.GetUtcNow().UtcDateTime);
        }

        if (await ShouldCreateDefaultCollectionAsync(request, wasDemotedFromPrivilegedRole))
        {
            await collectionRepository.CreateDefaultCollectionsAsync(
                organizationUser.OrganizationId,
                [organizationUser.Id],
                request.DefaultUserCollectionName!);
        }

        await eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Updated);

        return new None();
    }

    private static bool IsEnablingSecretsManager(UpdateOrganizationUserRequest request) =>
        !request.OrganizationUserToUpdate.AccessSecretsManager && request.NewAccessSecretsManager;

    private static bool IsDemotingFromPrivilegedRole(UpdateOrganizationUserRequest request) =>
        request.OrganizationUserToUpdate.Type is OrganizationUserType.Admin or OrganizationUserType.Owner
        && request.NewType is not (OrganizationUserType.Admin or OrganizationUserType.Owner);

    private async Task<CommandError?> TryEnablingSecretsManagerAsync(UpdateOrganizationUserRequest request)
    {
        var organization = request.Organization;
        var additionalSmSeatsRequired = await countNewSmSeatsRequiredQuery.CountNewSmSeatsRequiredAsync(organization.Id, 1);
        if (additionalSmSeatsRequired > 0)
        {
            // Self-hosted instances can't autoscale their Stripe subscription; reject rather than attempt a purchase.
            if (globalSettings.SelfHosted)
            {
                return new CannotAutoscaleSecretsManagerSeatsOnSelfHost();
            }

            try
            {
                var plan = await pricingClient.GetPlanOrThrow(organization.PlanType);
                var update = new SecretsManagerSubscriptionUpdate(organization, plan, true)
                    .AdjustSeats(additionalSmSeatsRequired);
                await updateSecretsManagerSubscriptionCommand.UpdateSubscriptionAsync(update);
            }
            catch (BadRequestException ex)
            {
                return new CouldNotIncreaseSeatsOfSecretManager(ex.Message);
            }
        }

        return null;
    }

    private async Task<bool> ShouldCreateDefaultCollectionAsync(UpdateOrganizationUserRequest request,
        bool wasDemotedFromPrivilegedRole) =>
        wasDemotedFromPrivilegedRole
        && request.OrganizationUserToUpdate.UserId.HasValue
        && request.Organization.UseMyItems
        && !string.IsNullOrWhiteSpace(request.DefaultUserCollectionName)
        && (await policyRequirementQuery.GetAsyncVNext<OrganizationDataOwnershipPolicyRequirement>(
            request.OrganizationUserToUpdate.UserId!.Value))
        .State == OrganizationDataOwnershipState.Enabled;
}
