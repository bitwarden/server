﻿using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptions.Interface;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;

namespace Bit.Core.OrganizationFeatures.OrganizationSubscriptions;

public class UpdateSecretsManagerSubscriptionCommand : IUpdateSecretsManagerSubscriptionCommand
{
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IPaymentService _paymentService;
    private readonly IMailService _mailService;
    private readonly ILogger<UpdateSecretsManagerSubscriptionCommand> _logger;
    private readonly IServiceAccountRepository _serviceAccountRepository;
    private readonly IGlobalSettings _globalSettings;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly IEventService _eventService;

    public UpdateSecretsManagerSubscriptionCommand(
        IOrganizationUserRepository organizationUserRepository,
        IPaymentService paymentService,
        IMailService mailService,
        ILogger<UpdateSecretsManagerSubscriptionCommand> logger,
        IServiceAccountRepository serviceAccountRepository,
        IGlobalSettings globalSettings,
        IOrganizationRepository organizationRepository,
        IApplicationCacheService applicationCacheService,
        IEventService eventService)
    {
        _organizationUserRepository = organizationUserRepository;
        _paymentService = paymentService;
        _mailService = mailService;
        _logger = logger;
        _serviceAccountRepository = serviceAccountRepository;
        _globalSettings = globalSettings;
        _organizationRepository = organizationRepository;
        _applicationCacheService = applicationCacheService;
        _eventService = eventService;
    }

    public async Task UpdateSubscriptionAsync(SecretsManagerSubscriptionUpdate update)
    {
        await ValidateUpdateAsync(update);

        await FinalizeSubscriptionAdjustmentAsync(update);

        if (update.SmSeatAutoscaleLimitReached)
        {
            await SendSeatLimitEmailAsync(update.Organization);
        }

        if (update.SmServiceAccountAutoscaleLimitReached)
        {
            await SendServiceAccountLimitEmailAsync(update.Organization);
        }
    }

    private async Task FinalizeSubscriptionAdjustmentAsync(SecretsManagerSubscriptionUpdate update)
    {
        if (update.SmSeatsChanged)
        {
            await _paymentService.AdjustSmSeatsAsync(update.Organization, update.Plan, update.SmSeatsExcludingBase);

            // TODO: call ReferenceEventService - see AC-1481
        }

        if (update.SmServiceAccountsChanged)
        {
            await _paymentService.AdjustServiceAccountsAsync(update.Organization, update.Plan,
                update.SmServiceAccountsExcludingBase);

            // TODO: call ReferenceEventService - see AC-1481
        }

        var organization = update.Organization;
        organization.SmSeats = update.SmSeats;
        organization.SmServiceAccounts = update.SmServiceAccounts;
        organization.MaxAutoscaleSmSeats = update.MaxAutoscaleSmSeats;
        organization.MaxAutoscaleSmServiceAccounts = update.MaxAutoscaleSmServiceAccounts;

        await ReplaceAndUpdateCacheAsync(organization);
    }

    private async Task SendSeatLimitEmailAsync(Organization organization)
    {
        try
        {
            var ownerEmails = (await _organizationUserRepository.GetManyByMinimumRoleAsync(organization.Id,
                    OrganizationUserType.Owner))
                .Select(u => u.Email).Distinct();

            await _mailService.SendSecretsManagerMaxSeatLimitReachedEmailAsync(organization, organization.MaxAutoscaleSmSeats.Value, ownerEmails);

        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Error encountered notifying organization owners of seats limit reached.");
        }
    }

    private async Task SendServiceAccountLimitEmailAsync(Organization organization)
    {
        try
        {
            var ownerEmails = (await _organizationUserRepository.GetManyByMinimumRoleAsync(organization.Id,
                    OrganizationUserType.Owner))
                .Select(u => u.Email).Distinct();

            await _mailService.SendSecretsManagerMaxServiceAccountLimitReachedEmailAsync(organization, organization.MaxAutoscaleSmServiceAccounts.Value, ownerEmails);

        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Error encountered notifying organization owners of machine accounts limit reached.");
        }

    }

    public async Task ValidateUpdateAsync(SecretsManagerSubscriptionUpdate update)
    {
        if (_globalSettings.SelfHosted)
        {
            var message = update.Autoscaling
                ? "Cannot autoscale on a self-hosted instance."
                : "Cannot update subscription on a self-hosted instance.";
            throw new BadRequestException(message);
        }

        ValidateOrganization(update);

        if (update.SmSeatsChanged)
        {
            await ValidateSmSeatsUpdateAsync(update);
        }

        if (update.SmServiceAccountsChanged)
        {
            await ValidateSmServiceAccountsUpdateAsync(update);
        }

        if (update.MaxAutoscaleSmSeatsChanged)
        {
            ValidateMaxAutoscaleSmSeatsUpdateAsync(update);
        }

        if (update.MaxAutoscaleSmServiceAccountsChanged)
        {
            ValidateMaxAutoscaleSmServiceAccountUpdate(update);
        }
    }

    private void ValidateOrganization(SecretsManagerSubscriptionUpdate update)
    {
        var organization = update.Organization;

        if (!organization.UseSecretsManager)
        {
            throw new BadRequestException("Organization has no access to Secrets Manager.");
        }

        if (update.Plan.ProductTier == ProductTierType.Free)
        {
            // No need to check the organization is set up with Stripe
            return;
        }

        if (string.IsNullOrWhiteSpace(organization.GatewayCustomerId))
        {
            throw new BadRequestException("No payment method found.");
        }

        if (string.IsNullOrWhiteSpace(organization.GatewaySubscriptionId))
        {
            throw new BadRequestException("No subscription found.");
        }
    }

    private async Task ValidateSmSeatsUpdateAsync(SecretsManagerSubscriptionUpdate update)
    {
        var organization = update.Organization;
        var plan = update.Plan;

        // Check if the organization has unlimited seats
        if (organization.SmSeats == null)
        {
            throw new BadRequestException("Organization has no Secrets Manager seat limit, no need to adjust seats");
        }

        if (update.Autoscaling && update.SmSeats.Value < organization.SmSeats.Value)
        {
            throw new BadRequestException("Cannot use autoscaling to subtract seats.");
        }

        // Check plan maximum seats
        if (!plan.SecretsManager.HasAdditionalSeatsOption ||
            (plan.SecretsManager.MaxAdditionalSeats.HasValue && update.SmSeatsExcludingBase > plan.SecretsManager.MaxAdditionalSeats.Value))
        {
            var planMaxSeats = plan.SecretsManager.BaseSeats + plan.SecretsManager.MaxAdditionalSeats.GetValueOrDefault();
            throw new BadRequestException($"You have reached the maximum number of Secrets Manager seats ({planMaxSeats}) for this plan.");
        }

        // Check autoscale maximum seats
        if (update.MaxAutoscaleSmSeats.HasValue && update.SmSeats.Value > update.MaxAutoscaleSmSeats.Value)
        {
            var message = update.Autoscaling
                ? "Secrets Manager seat limit has been reached."
                : "Cannot set max seat autoscaling below seat count.";
            throw new BadRequestException(message);
        }

        // Check minimum seats included with plan
        if (plan.SecretsManager.BaseSeats > update.SmSeats.Value)
        {
            throw new BadRequestException($"Plan has a minimum of {plan.SecretsManager.BaseSeats} Secrets Manager  seats.");
        }

        // Check minimum seats required by business logic
        if (update.SmSeats.Value <= 0)
        {
            throw new BadRequestException("You must have at least 1 Secrets Manager seat.");
        }

        // Check minimum seats currently in use by the organization
        if (organization.SmSeats.Value > update.SmSeats.Value)
        {
            var occupiedSeats = await _organizationUserRepository.GetOccupiedSmSeatCountByOrganizationIdAsync(organization.Id);
            if (occupiedSeats > update.SmSeats.Value)
            {
                throw new BadRequestException($"{occupiedSeats} users are currently occupying Secrets Manager seats. " +
                                              "You cannot decrease your subscription below your current occupied seat count.");
            }
        }

        // Check that SM seats aren't greater than password manager seats
        if (organization.Seats < update.SmSeats.Value)
        {
            throw new BadRequestException("You cannot have more Secrets Manager seats than Password Manager seats.");
        }
    }

    private async Task ValidateSmServiceAccountsUpdateAsync(SecretsManagerSubscriptionUpdate update)
    {
        var organization = update.Organization;
        var plan = update.Plan;

        // Check if the organization has unlimited service accounts
        if (organization.SmServiceAccounts == null)
        {
            throw new BadRequestException("Organization has no machine accounts limit, no need to adjust machine accounts");
        }

        if (update.Autoscaling && update.SmServiceAccounts.Value < organization.SmServiceAccounts.Value)
        {
            throw new BadRequestException("Cannot use autoscaling to subtract machine accounts.");
        }

        // Check plan maximum service accounts
        if (!plan.SecretsManager.HasAdditionalServiceAccountOption ||
            (plan.SecretsManager.MaxAdditionalServiceAccount.HasValue && update.SmServiceAccountsExcludingBase > plan.SecretsManager.MaxAdditionalServiceAccount.Value))
        {
            var planMaxServiceAccounts = plan.SecretsManager.BaseServiceAccount +
                                         plan.SecretsManager.MaxAdditionalServiceAccount.GetValueOrDefault();
            throw new BadRequestException($"You have reached the maximum number of machine accounts ({planMaxServiceAccounts}) for this plan.");
        }

        // Check autoscale maximum service accounts
        if (update.MaxAutoscaleSmServiceAccounts.HasValue &&
            update.SmServiceAccounts.Value > update.MaxAutoscaleSmServiceAccounts.Value)
        {
            var message = update.Autoscaling
                ? "Secrets Manager machine account limit has been reached."
                : "Cannot set max machine accounts autoscaling below machine account amount.";
            throw new BadRequestException(message);
        }

        // Check minimum service accounts included with plan
        if (plan.SecretsManager.BaseServiceAccount > update.SmServiceAccounts.Value)
        {
            throw new BadRequestException($"Plan has a minimum of {plan.SecretsManager.BaseServiceAccount} machine accounts.");
        }

        // Check minimum service accounts required by business logic
        if (update.SmServiceAccounts.Value <= 0)
        {
            throw new BadRequestException("You must have at least 1 machine account.");
        }

        // Check minimum service accounts currently in use by the organization
        if (!organization.SmServiceAccounts.HasValue || organization.SmServiceAccounts.Value > update.SmServiceAccounts.Value)
        {
            var currentServiceAccounts = await _serviceAccountRepository.GetServiceAccountCountByOrganizationIdAsync(organization.Id);
            if (currentServiceAccounts > update.SmServiceAccounts)
            {
                throw new BadRequestException($"Your organization currently has {currentServiceAccounts} machine accounts. " +
                                              $"You cannot decrease your subscription below your current machine account usage.");
            }
        }
    }

    private void ValidateMaxAutoscaleSmSeatsUpdateAsync(SecretsManagerSubscriptionUpdate update)
    {
        var plan = update.Plan;

        if (!update.MaxAutoscaleSmSeats.HasValue)
        {
            // autoscale limit has been turned off, no validation required
            return;
        }

        if (update.SmSeats.HasValue && update.MaxAutoscaleSmSeats.Value < update.SmSeats.Value)
        {
            throw new BadRequestException($"Cannot set max Secrets Manager seat autoscaling below current Secrets Manager seat count.");
        }

        if (plan.SecretsManager.MaxSeats.HasValue && update.MaxAutoscaleSmSeats.Value > plan.SecretsManager.MaxSeats)
        {
            throw new BadRequestException(string.Concat(
                $"Your plan has a Secrets Manager seat limit of {plan.SecretsManager.MaxSeats}, ",
                $"but you have specified a max autoscale count of {update.MaxAutoscaleSmSeats}.",
                "Reduce your max autoscale count."));
        }

        if (!plan.SecretsManager.AllowSeatAutoscale)
        {
            throw new BadRequestException("Your plan does not allow Secrets Manager seat autoscaling.");
        }
    }

    private void ValidateMaxAutoscaleSmServiceAccountUpdate(SecretsManagerSubscriptionUpdate update)
    {
        var plan = update.Plan;

        if (!update.MaxAutoscaleSmServiceAccounts.HasValue)
        {
            // autoscale limit has been turned off, no validation required
            return;
        }

        if (update.SmServiceAccounts.HasValue && update.MaxAutoscaleSmServiceAccounts.Value < update.SmServiceAccounts.Value)
        {
            throw new BadRequestException(
                $"Cannot set max machine accounts autoscaling below current machine accounts count.");
        }

        if (!plan.SecretsManager.AllowServiceAccountsAutoscale)
        {
            throw new BadRequestException("Your plan does not allow machine accounts autoscaling.");
        }

        if (plan.SecretsManager.MaxServiceAccounts.HasValue && update.MaxAutoscaleSmServiceAccounts.Value > plan.SecretsManager.MaxServiceAccounts)
        {
            throw new BadRequestException(string.Concat(
                $"Your plan has a machine account limit of {plan.SecretsManager.MaxServiceAccounts}, ",
                $"but you have specified a max autoscale count of {update.MaxAutoscaleSmServiceAccounts}.",
                "Reduce your max autoscale count."));
        }
    }

    // TODO: This is a temporary duplication of OrganizationService.ReplaceAndUpdateCache to avoid a circular dependency.
    // TODO: This should no longer be necessary when user-related methods are extracted from OrganizationService: see PM-1880
    private async Task ReplaceAndUpdateCacheAsync(Organization org, EventType? orgEvent = null)
    {
        await _organizationRepository.ReplaceAsync(org);
        await _applicationCacheService.UpsertOrganizationAbilityAsync(org);

        if (orgEvent.HasValue)
        {
            await _eventService.LogOrganizationEventAsync(org, orgEvent.Value);
        }
    }
}
