﻿using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Models;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using static Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.InviteUserValidationErrorMessages;
using SecretsManagerSubscriptionUpdate = Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Models.SecretsManagerSubscriptionUpdate;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation;

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

        if (InvitingUserOrganizationValidation.Validate(request.Organization) is Invalid<OrganizationDto> organizationValidation)
        {
            return new Invalid<InviteUserOrganizationValidationRequest>(organizationValidation.ErrorMessageString);
        }

        var subscriptionUpdate = PasswordManagerSubscriptionUpdate.Create(request);

        if (PasswordManagerInviteUserValidation.Validate(subscriptionUpdate) is
            Invalid<PasswordManagerSubscriptionUpdate> invalidSubscriptionUpdate)
        {
            return new Invalid<InviteUserOrganizationValidationRequest>(invalidSubscriptionUpdate.ErrorMessageString);
        }

        var smSubscriptionUpdate = SecretsManagerSubscriptionUpdate.Create(request, subscriptionUpdate);

        if (SecretsManagerInviteUserValidation.Validate(smSubscriptionUpdate) is
            Invalid<SecretsManagerSubscriptionUpdate> invalidSmSubscriptionUpdate)
        {
            return new Invalid<InviteUserOrganizationValidationRequest>(invalidSmSubscriptionUpdate.ErrorMessageString);
        }

        var provider = await providerRepository.GetByOrganizationIdAsync(request.Organization.OrganizationId);

        if (InvitingUserOrganizationProviderValidation.Validate(ProviderDto.FromProviderEntity(provider)) is
            Invalid<ProviderDto> invalidProviderValidation)
        {
            return new Invalid<InviteUserOrganizationValidationRequest>(invalidProviderValidation.ErrorMessageString);
        }

        var paymentSubscription = await paymentService.GetSubscriptionAsync(await organizationRepository.GetByIdAsync(request.Organization.OrganizationId));

        if (InviteUserPaymentValidation.Validate(PaymentSubscriptionDto.FromSubscriptionInfo(paymentSubscription, request.Organization)) is
            Invalid<PaymentSubscriptionDto> invalidPaymentValidation)
        {
            return new Invalid<InviteUserOrganizationValidationRequest>(invalidPaymentValidation.ErrorMessageString);
        }

        return new Valid<InviteUserOrganizationValidationRequest>(null);
    }

    public static ValidationResult<IGlobalSettings> ValidateEnvironment(IGlobalSettings globalSettings) =>
        globalSettings.SelfHosted
            ? new Invalid<IGlobalSettings>(CannotAutoScaleOnSelfHostedError)
            : new Valid<IGlobalSettings>(globalSettings);
}
