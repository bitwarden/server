// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.GlobalSettings;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Organization;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Payments;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.Validation;
using Bit.Core.Billing.Services;
using Bit.Core.Repositories;
using Bit.Core.Settings;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.PasswordManager;

public interface IInviteUsersPasswordManagerValidator : IValidator<PasswordManagerSubscriptionUpdate>;

public class InviteUsersPasswordManagerValidator(
    IGlobalSettings globalSettings,
    IInviteUsersEnvironmentValidator inviteUsersEnvironmentValidator,
    IInviteUsersOrganizationValidator inviteUsersOrganizationValidator,
    IProviderRepository providerRepository,
    IStripePaymentService paymentService,
    IOrganizationRepository organizationRepository
    ) : IInviteUsersPasswordManagerValidator
{
    /// <summary>
    /// This is for validating if the organization can add additional users.
    /// </summary>
    /// <param name="subscriptionUpdate"></param>
    /// <returns></returns>
    public static ValidationResult<PasswordManagerSubscriptionUpdate> ValidatePasswordManager(PasswordManagerSubscriptionUpdate subscriptionUpdate)
    {
        if (subscriptionUpdate.Seats is null)
        {
            return new Valid<PasswordManagerSubscriptionUpdate>(subscriptionUpdate);
        }

        if (subscriptionUpdate.SeatsRequiredToAdd == 0)
        {
            return new Valid<PasswordManagerSubscriptionUpdate>(subscriptionUpdate);
        }

        if (subscriptionUpdate.PasswordManagerPlan.BaseSeats + subscriptionUpdate.SeatsRequiredToAdd <= 0)
        {
            return new Invalid<PasswordManagerSubscriptionUpdate>(new PasswordManagerMustHaveSeatsError(subscriptionUpdate));
        }

        if (subscriptionUpdate.MaxSeatsExceeded)
        {
            return new Invalid<PasswordManagerSubscriptionUpdate>(
                new PasswordManagerSeatLimitHasBeenReachedError(subscriptionUpdate));
        }

        if (subscriptionUpdate.PasswordManagerPlan.HasAdditionalSeatsOption is false)
        {
            return new Invalid<PasswordManagerSubscriptionUpdate>(
                new PasswordManagerPlanDoesNotAllowAdditionalSeatsError(subscriptionUpdate));
        }

        // Apparently MaxAdditionalSeats is never set. Can probably be removed.
        if (subscriptionUpdate.UpdatedSeatTotal - subscriptionUpdate.PasswordManagerPlan.BaseSeats > subscriptionUpdate.PasswordManagerPlan.MaxAdditionalSeats)
        {
            return new Invalid<PasswordManagerSubscriptionUpdate>(
                new PasswordManagerPlanOnlyAllowsMaxAdditionalSeatsError(subscriptionUpdate));
        }

        return new Valid<PasswordManagerSubscriptionUpdate>(subscriptionUpdate);
    }

    public async Task<ValidationResult<PasswordManagerSubscriptionUpdate>> ValidateAsync(PasswordManagerSubscriptionUpdate request)
    {
        switch (ValidatePasswordManager(request))
        {
            case Valid<PasswordManagerSubscriptionUpdate> valid
                when valid.Value.SeatsRequiredToAdd is 0:
                return new Valid<PasswordManagerSubscriptionUpdate>(request);

            case Invalid<PasswordManagerSubscriptionUpdate> invalid:
                return invalid;
        }

        if (await inviteUsersEnvironmentValidator.ValidateAsync(new EnvironmentRequest(globalSettings, request)) is Invalid<EnvironmentRequest> invalidEnvironment)
        {
            return invalidEnvironment.Map(request);
        }

        // Organizations managed by a provider need to be scaled by the provider. This needs to be checked in the event seats are increasing.
        var provider = await providerRepository.GetByOrganizationIdAsync(request.InviteOrganization.OrganizationId);

        if (provider is not null)
        {
            var providerValidationResult = InvitingUserOrganizationProviderValidator.Validate(new InviteOrganizationProvider(provider));

            if (providerValidationResult is Invalid<InviteOrganizationProvider> invalidProviderValidation)
            {
                return invalidProviderValidation.Map(request);
            }
        }

        var organizationValidationResult = await inviteUsersOrganizationValidator.ValidateAsync(request.InviteOrganization);

        if (organizationValidationResult is Invalid<InviteOrganization> organizationValidation)
        {
            return organizationValidation.Map(request);
        }

        var paymentSubscription = await paymentService.GetSubscriptionAsync(
            await organizationRepository.GetByIdAsync(request.InviteOrganization.OrganizationId));

        var paymentValidationResult = InviteUserPaymentValidation.Validate(
            new PaymentsSubscription(paymentSubscription, request.InviteOrganization));

        if (paymentValidationResult is Invalid<PaymentsSubscription> invalidPaymentValidation)
        {
            return invalidPaymentValidation.Map(request);
        }

        return new Valid<PasswordManagerSubscriptionUpdate>(request);
    }
}
