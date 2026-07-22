using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Auth.UserFeatures.UserEmail;
using Bit.Core.Billing.Pricing;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptions.Interface;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
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
    IUserRepository userRepository,
    IChangeEmailCommand changeEmailCommand,
    IPushNotificationService pushNotificationService,
    TimeProvider timeProvider)
    : IUpdateOrganizationUserCommand
{
    public async Task<CommandResult> UpdateUserAsync(UpdateOrganizationUserRequest request)
    {
        request = await LoadUserToUpdateAsync(request);

        var validationResult = await validator.ValidateAsync(request);

        if (validationResult.IsError)
        {
            return validationResult.AsError;
        }

        var organizationUser = request.OrganizationUserToUpdate.UpdateOrganizationUser(request.NewType,
            request.NewPermissions,
            request.NewAccessSecretsManager,
            timeProvider);

        if (request.IsEnablingSecretsManager())
        {
            var commandError = await TryEnablingSecretsManagerAsync(request);
            if (commandError is not null)
            {
                return commandError;
            }
        }

        if (request.IsEmailChanged() || request.IsNameChanged())
        {
            var commandError = await TryApplyAccountChangesAsync(request);
            if (commandError is not null)
            {
                return commandError;
            }
        }

        await organizationUserRepository.ReplaceAsync(organizationUser, request.Collections.collectionAccessToSave?.ToList() ?? []);

        if (request.NewGroups != null)
        {
            await organizationUserRepository.UpdateGroupsAsync(organizationUser.Id, request.NewGroups,
                timeProvider.GetUtcNow().UtcDateTime);
        }

        if (await ShouldCreateDefaultCollectionAsync(request))
        {
            await collectionRepository.CreateDefaultCollectionsAsync(
                organizationUser.OrganizationId,
                [organizationUser.Id],
                request.DefaultUserCollectionName!);
        }

        await eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Updated);

        return new None();
    }

    private async Task<CommandError?> TryApplyAccountChangesAsync(UpdateOrganizationUserRequest request)
    {
        if (request.UserToUpdate is null)
        {
            return null;
        }

        try
        {
            var userToUpdate = request.UserToUpdate;

            if (request.IsNameChanged())
            {
                userToUpdate.Name = request.NormalizedNewName;
            }

            if (request.IsEmailChanged())
            {
                // ChangeEmailAsync persists the account (including any name change above) and syncs Stripe.
                await changeEmailCommand.ChangeEmailAsync(request.UserToUpdate, request.NewEmail!);
            }
            else
            {
                userToUpdate.RevisionDate = userToUpdate.AccountRevisionDate = timeProvider.GetUtcNow().UtcDateTime;
                await userRepository.ReplaceAsync(userToUpdate);
            }

            // Notify the member's devices that their account state changed so clients re-sync.
            await pushNotificationService.PushSyncSettingsAsync(userToUpdate.Id);
            return null;
        }
        catch (BadRequestException ex)
        {
            return MapEmailChangeError(ex);
        }
    }

    // Map known errors and passthrough unknown errors.
    private static CommandError MapEmailChangeError(BadRequestException ex) => ex.Message switch
    {
        ChangeEmailCommand.EmailAlreadyInUseError => new EmailAlreadyInUseError(),
        OrganizationDomainAllowEmailChangeQuery.EmailClaimedByOrganizationError => new EmailClaimedByAnotherOrganizationError(),
        OrganizationDomainAllowEmailChangeQuery.EmailNotOnVerifiedDomainError => new NewEmailDomainNotClaimedError(),
        _ => new EmailChangeFailedError(ex.Message)
    };

    private async Task<CommandError?> TryEnablingSecretsManagerAsync(UpdateOrganizationUserRequest request)
    {
        var organization = request.Organization;
        var additionalSmSeatsRequired = await countNewSmSeatsRequiredQuery.CountNewSmSeatsRequiredAsync(organization.Id, 1);
        if (additionalSmSeatsRequired > 0)
        {
            // Self-hosted instances can't autoscale their Stripe subscription.
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

    private async Task<UpdateOrganizationUserRequest> LoadUserToUpdateAsync(UpdateOrganizationUserRequest request)
    {
        var wantsAccountChange = !string.IsNullOrWhiteSpace(request.NewEmail) || request.NewName is not null;
        if (!wantsAccountChange || !request.OrganizationUserToUpdate.UserId.HasValue)
        {
            return request;
        }

        var userToUpdate = await userRepository.GetByIdAsync(request.OrganizationUserToUpdate.UserId.Value);
        return request with { UserToUpdate = userToUpdate };
    }

    private async Task<bool> ShouldCreateDefaultCollectionAsync(UpdateOrganizationUserRequest request) =>
        request.IsDemotedFromPrivilegedRole()
        && request.OrganizationUserToUpdate.UserId.HasValue
        && request.Organization.UseMyItems
        && !string.IsNullOrWhiteSpace(request.DefaultUserCollectionName)
        && (await policyRequirementQuery
            .GetAsyncVNext<OrganizationDataOwnershipPolicyRequirement>(request.OrganizationUserToUpdate.UserId.Value))
        .GetDefaultCollectionRequestOnUpdate(request.Organization.Id);
}
