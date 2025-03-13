using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Models;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using static Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.InviteUserValidationErrorMessages;
using SecretsManagerSubscriptionUpdate = Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Models.SecretsManagerSubscriptionUpdate;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation;

// TODO move into own file ... and change name to validator
public interface IInviteUsersValidation
{
    Task<ValidationResult<InviteUserOrganizationValidationRequest>> ValidateAsync(InviteUserOrganizationValidationRequest request);
}

public class InviteUsersValidation(
    IGlobalSettings globalSettings,
    IProviderRepository providerRepository,
    IPaymentService paymentService,
    IOrganizationRepository organizationRepository) : IInviteUsersValidation
{
    public async Task<ValidationResult<InviteUserOrganizationValidationRequest>> ValidateAsync(InviteUserOrganizationValidationRequest request)
    {
        if (ValidateEnvironment(globalSettings) is Invalid<IGlobalSettings> invalidEnvironment)
        {
            return new Invalid<InviteUserOrganizationValidationRequest>(invalidEnvironment.ErrorMessageString);
        }

        var organizationValidationResult = InvitingUserOrganizationValidation.Validate(request.InviteOrganization);

        if (organizationValidationResult is Invalid<InviteOrganization> organizationValidation)
        {
            return new Invalid<InviteUserOrganizationValidationRequest>(organizationValidation.ErrorMessageString);
        }

        var subscriptionUpdate = new PasswordManagerSubscriptionUpdate(request);
        var passwordManagerValidationResult = PasswordManagerInviteUserValidation.Validate(subscriptionUpdate);

        if (passwordManagerValidationResult is Invalid<PasswordManagerSubscriptionUpdate> invalidSubscriptionUpdate)
        {
            return new Invalid<InviteUserOrganizationValidationRequest>(invalidSubscriptionUpdate.ErrorMessageString);
        }

        var smSubscriptionUpdate = new SecretsManagerSubscriptionUpdate(request, subscriptionUpdate);
        var secretsManagerValidationResult = SecretsManagerInviteUserValidation.Validate(smSubscriptionUpdate);

        if (secretsManagerValidationResult is Invalid<SecretsManagerSubscriptionUpdate> invalidSmSubscriptionUpdate)
        {
            return new Invalid<InviteUserOrganizationValidationRequest>(invalidSmSubscriptionUpdate.ErrorMessageString);
        }

        var provider = await providerRepository.GetByOrganizationIdAsync(request.InviteOrganization.OrganizationId);
        if (provider is not null)
        {
            var providerValidationResult = InvitingUserOrganizationProviderValidation.Validate(ProviderDto.FromProviderEntity(provider));

            if (providerValidationResult is Invalid<ProviderDto> invalidProviderValidation)
            {
                return new Invalid<InviteUserOrganizationValidationRequest>(invalidProviderValidation.ErrorMessageString);
            }
        }

        var paymentSubscription = await paymentService.GetSubscriptionAsync(await organizationRepository.GetByIdAsync(request.InviteOrganization.OrganizationId));
        var paymentValidationResult = InviteUserPaymentValidation.Validate(new PaymentsSubscription(paymentSubscription, request.InviteOrganization));

        if (paymentValidationResult is Invalid<PaymentsSubscription> invalidPaymentValidation)
        {
            return new Invalid<InviteUserOrganizationValidationRequest>(invalidPaymentValidation.ErrorMessageString);
        }

        return new Valid<InviteUserOrganizationValidationRequest>(new InviteUserOrganizationValidationRequest(
            request,
            subscriptionUpdate,
            smSubscriptionUpdate));
    }

    public static ValidationResult<IGlobalSettings> ValidateEnvironment(IGlobalSettings globalSettings) =>
        globalSettings.SelfHosted
            ? new Invalid<IGlobalSettings>(CannotAutoScaleOnSelfHostedError)
            : new Valid<IGlobalSettings>(globalSettings);
}
