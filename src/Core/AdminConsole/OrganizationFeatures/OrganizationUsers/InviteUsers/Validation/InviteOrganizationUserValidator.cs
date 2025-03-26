using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.GlobalSettings;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Organization;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.PasswordManager;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Provider;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.SecretsManager;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Shared.Validation;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using SecretsManagerSubscriptionUpdate = Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.SecretsManager.SecretsManagerSubscriptionUpdate;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation;

public interface IInviteUsersValidator : IValidator<InviteUserOrganizationValidationRequest>;

public class InviteUsersValidator(
    IGlobalSettings globalSettings,
    IProviderRepository providerRepository,
    IPaymentService paymentService,
    IOrganizationRepository organizationRepository) : IInviteUsersValidator
{
    public async Task<ValidationResult<InviteUserOrganizationValidationRequest>> ValidateAsync(InviteUserOrganizationValidationRequest request)
    {
        var subscriptionUpdate = new PasswordManagerSubscriptionUpdate(request);
        var passwordManagerValidationResult = PasswordManagerInviteUserValidator.Validate(subscriptionUpdate);

        if (passwordManagerValidationResult is Invalid<PasswordManagerSubscriptionUpdate> invalidSubscriptionUpdate)
        {
            return invalidSubscriptionUpdate.Map(request);
        }

        if (ValidateEnvironment(globalSettings, passwordManagerValidationResult as Valid<PasswordManagerSubscriptionUpdate>) is Invalid<IGlobalSettings> invalidEnvironment)
        {
            return invalidEnvironment.Map(request);
        }

        var organizationValidationResult = InviteUserOrganizationValidator.Validate(request.InviteOrganization, passwordManagerValidationResult as Valid<PasswordManagerSubscriptionUpdate>);

        if (organizationValidationResult is Invalid<InviteOrganization> organizationValidation)
        {
            return organizationValidation.Map(request);
        }

        var smSubscriptionUpdate = new SecretsManagerSubscriptionUpdate(request, subscriptionUpdate);

        var secretsManagerValidationResult = SecretsManagerInviteUserValidation.Validate(smSubscriptionUpdate);

        if (secretsManagerValidationResult is Invalid<SecretsManagerSubscriptionUpdate> invalidSmSubscriptionUpdate)
        {
            return invalidSmSubscriptionUpdate.Map(request);
        }

        var provider = await providerRepository.GetByOrganizationIdAsync(request.InviteOrganization.OrganizationId);
        if (provider is not null)
        {
            var providerValidationResult = InvitingUserOrganizationProviderValidator.Validate(new InviteOrganizationProvider(provider));

            if (providerValidationResult is Invalid<InviteOrganizationProvider> invalidProviderValidation)
            {
                return invalidProviderValidation.Map(request);
            }
        }

        var paymentSubscription = await paymentService.GetSubscriptionAsync(
            await organizationRepository.GetByIdAsync(request.InviteOrganization.OrganizationId));

        var paymentValidationResult = InviteUserPaymentValidation.Validate(
            new PaymentsSubscription(paymentSubscription, request.InviteOrganization));

        if (paymentValidationResult is Invalid<PaymentsSubscription> invalidPaymentValidation)
        {
            return invalidPaymentValidation.Map(request);
        }

        return new Valid<InviteUserOrganizationValidationRequest>(new InviteUserOrganizationValidationRequest(
            request,
            subscriptionUpdate,
            smSubscriptionUpdate));
    }

    public static ValidationResult<IGlobalSettings> ValidateEnvironment(IGlobalSettings globalSettings, Valid<PasswordManagerSubscriptionUpdate> subscriptionUpdate) =>
        globalSettings.SelfHosted && subscriptionUpdate?.Value.SeatsRequiredToAdd > 0
            ? new Invalid<IGlobalSettings>(new CannotAutoScaleOnSelfHostError(globalSettings))
            : new Valid<IGlobalSettings>(globalSettings);
}
