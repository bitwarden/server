﻿using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.AdminConsole.Models.Response;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Api.Models.Request;
using Bit.Api.Models.Request.Organizations;
using Bit.Api.Models.Response;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Entities;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Repositories;
using Bit.Core.Billing.Services;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.OrganizationFeatures.OrganizationLicenses.Interfaces;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptions.Interface;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Billing.Controllers;

[Route("organizations")]
[Authorize("Application")]
public class OrganizationsController(
    IOrganizationRepository organizationRepository,
    IOrganizationUserRepository organizationUserRepository,
    IOrganizationService organizationService,
    IUserService userService,
    IPaymentService paymentService,
    ICurrentContext currentContext,
    ICloudGetOrganizationLicenseQuery cloudGetOrganizationLicenseQuery,
    GlobalSettings globalSettings,
    ILicensingService licensingService,
    IUpdateSecretsManagerSubscriptionCommand updateSecretsManagerSubscriptionCommand,
    IUpgradeOrganizationPlanCommand upgradeOrganizationPlanCommand,
    IAddSecretsManagerSubscriptionCommand addSecretsManagerSubscriptionCommand,
    IReferenceEventService referenceEventService,
    ISubscriberService subscriberService,
    IOrganizationInstallationRepository organizationInstallationRepository,
    IPricingClient pricingClient)
    : Controller
{
    [HttpGet("{id:guid}/subscription")]
    public async Task<OrganizationSubscriptionResponseModel> GetSubscription(Guid id)
    {
        if (!await currentContext.ViewSubscription(id))
        {
            throw new NotFoundException();
        }

        var organization = await organizationRepository.GetByIdAsync(id);
        if (organization == null)
        {
            throw new NotFoundException();
        }

        if (globalSettings.SelfHosted)
        {
            var orgLicense = await licensingService.ReadOrganizationLicenseAsync(organization);
            return new OrganizationSubscriptionResponseModel(organization, orgLicense);
        }

        var plan = await pricingClient.GetPlanOrThrow(organization.PlanType);

        if (string.IsNullOrEmpty(organization.GatewaySubscriptionId))
        {
            return new OrganizationSubscriptionResponseModel(organization, plan);
        }

        var subscriptionInfo = await paymentService.GetSubscriptionAsync(organization);
        if (subscriptionInfo == null)
        {
            throw new NotFoundException();
        }

        var hideSensitiveData = !await currentContext.EditSubscription(id);

        return new OrganizationSubscriptionResponseModel(organization, subscriptionInfo, plan, hideSensitiveData);
    }

    [HttpGet("{id:guid}/license")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<OrganizationLicense> GetLicense(Guid id, [FromQuery] Guid installationId)
    {
        if (!await currentContext.OrganizationOwner(id))
        {
            throw new NotFoundException();
        }

        var org = await organizationRepository.GetByIdAsync(id);
        var license = await cloudGetOrganizationLicenseQuery.GetLicenseAsync(org, installationId);
        if (license == null)
        {
            throw new NotFoundException();
        }

        await SaveOrganizationInstallationAsync(id, installationId);

        return license;
    }

    [HttpPost("{id:guid}/payment")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task PostPayment(Guid id, [FromBody] PaymentRequestModel model)
    {
        if (!await currentContext.EditPaymentMethods(id))
        {
            throw new NotFoundException();
        }

        await organizationService.ReplacePaymentMethodAsync(id, model.PaymentToken,
            model.PaymentMethodType.Value, new TaxInfo
            {
                BillingAddressLine1 = model.Line1,
                BillingAddressLine2 = model.Line2,
                BillingAddressState = model.State,
                BillingAddressCity = model.City,
                BillingAddressPostalCode = model.PostalCode,
                BillingAddressCountry = model.Country,
                TaxIdNumber = model.TaxId,
            });
    }

    [HttpPost("{id:guid}/upgrade")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<PaymentResponseModel> PostUpgrade(Guid id, [FromBody] OrganizationUpgradeRequestModel model)
    {
        if (!await currentContext.EditSubscription(id))
        {
            throw new NotFoundException();
        }

        var (success, paymentIntentClientSecret) = await upgradeOrganizationPlanCommand.UpgradePlanAsync(id, model.ToOrganizationUpgrade());

        if (model.UseSecretsManager && success)
        {
            var userId = userService.GetProperUserId(User).Value;

            await TryGrantOwnerAccessToSecretsManagerAsync(id, userId);
        }

        return new PaymentResponseModel { Success = success, PaymentIntentClientSecret = paymentIntentClientSecret };
    }

    [HttpPost("{id}/sm-subscription")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<ProfileOrganizationResponseModel> PostSmSubscription(Guid id, [FromBody] SecretsManagerSubscriptionUpdateRequestModel model)
    {
        if (!await currentContext.EditSubscription(id))
        {
            throw new NotFoundException();
        }

        var organization = await organizationRepository.GetByIdAsync(id);
        if (organization == null)
        {
            throw new NotFoundException();
        }

        organization = await AdjustOrganizationSeatsForSmTrialAsync(id, organization, model);

        var plan = await pricingClient.GetPlanOrThrow(organization.PlanType);
        var organizationUpdate = model.ToSecretsManagerSubscriptionUpdate(organization, plan);

        await updateSecretsManagerSubscriptionCommand.UpdateSubscriptionAsync(organizationUpdate);

        var userId = userService.GetProperUserId(User)!.Value;

        return await GetProfileOrganizationResponseModelAsync(id, userId);
    }

    [HttpPost("{id:guid}/subscription")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<ProfileOrganizationResponseModel> PostSubscription(Guid id, [FromBody] OrganizationSubscriptionUpdateRequestModel model)
    {
        if (!await currentContext.EditSubscription(id))
        {
            throw new NotFoundException();
        }

        await organizationService.UpdateSubscription(id, model.SeatAdjustment, model.MaxAutoscaleSeats);

        var userId = userService.GetProperUserId(User)!.Value;

        return await GetProfileOrganizationResponseModelAsync(id, userId);
    }

    [HttpPost("{id:guid}/subscribe-secrets-manager")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<ProfileOrganizationResponseModel> PostSubscribeSecretsManagerAsync(Guid id, [FromBody] SecretsManagerSubscribeRequestModel model)
    {
        if (!await currentContext.EditSubscription(id))
        {
            throw new NotFoundException();
        }

        var organization = await organizationRepository.GetByIdAsync(id);
        if (organization == null)
        {
            throw new NotFoundException();
        }

        await addSecretsManagerSubscriptionCommand.SignUpAsync(organization, model.AdditionalSmSeats,
            model.AdditionalServiceAccounts);

        var userId = userService.GetProperUserId(User).Value;

        await TryGrantOwnerAccessToSecretsManagerAsync(organization.Id, userId);

        return await GetProfileOrganizationResponseModelAsync(organization.Id, userId);
    }

    [HttpPost("{id:guid}/seat")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<PaymentResponseModel> PostSeat(Guid id, [FromBody] OrganizationSeatRequestModel model)
    {
        if (!await currentContext.EditSubscription(id))
        {
            throw new NotFoundException();
        }

        var result = await organizationService.AdjustSeatsAsync(id, model.SeatAdjustment.Value);
        return new PaymentResponseModel { Success = true, PaymentIntentClientSecret = result };
    }

    [HttpPost("{id:guid}/verify-bank")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task PostVerifyBank(Guid id, [FromBody] OrganizationVerifyBankRequestModel model)
    {
        if (!await currentContext.EditSubscription(id))
        {
            throw new NotFoundException();
        }

        await organizationService.VerifyBankAsync(id, model.Amount1.Value, model.Amount2.Value);
    }

    [HttpPost("{id}/cancel")]
    public async Task PostCancel(Guid id, [FromBody] SubscriptionCancellationRequestModel request)
    {
        if (!await currentContext.EditSubscription(id))
        {
            throw new NotFoundException();
        }

        var organization = await organizationRepository.GetByIdAsync(id);

        if (organization == null)
        {
            throw new NotFoundException();
        }

        await subscriberService.CancelSubscription(organization,
            new OffboardingSurveyResponse
            {
                UserId = currentContext.UserId!.Value,
                Reason = request.Reason,
                Feedback = request.Feedback
            },
            organization.IsExpired());

        await referenceEventService.RaiseEventAsync(new ReferenceEvent(
            ReferenceEventType.CancelSubscription,
            organization,
            currentContext)
        {
            EndOfPeriod = organization.IsExpired()
        });
    }

    [HttpPost("{id:guid}/reinstate")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task PostReinstate(Guid id)
    {
        if (!await currentContext.EditSubscription(id))
        {
            throw new NotFoundException();
        }

        await organizationService.ReinstateSubscriptionAsync(id);
    }

    [HttpGet("{id:guid}/tax")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<TaxInfoResponseModel> GetTaxInfo(Guid id)
    {
        if (!await currentContext.OrganizationOwner(id))
        {
            throw new NotFoundException();
        }

        var organization = await organizationRepository.GetByIdAsync(id);
        if (organization == null)
        {
            throw new NotFoundException();
        }

        var taxInfo = await paymentService.GetTaxInfoAsync(organization);
        return new TaxInfoResponseModel(taxInfo);
    }

    [HttpPut("{id:guid}/tax")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task PutTaxInfo(Guid id, [FromBody] ExpandedTaxInfoUpdateRequestModel model)
    {
        if (!await currentContext.OrganizationOwner(id))
        {
            throw new NotFoundException();
        }

        var organization = await organizationRepository.GetByIdAsync(id);
        if (organization == null)
        {
            throw new NotFoundException();
        }

        var taxInfo = new TaxInfo
        {
            TaxIdNumber = model.TaxId,
            BillingAddressLine1 = model.Line1,
            BillingAddressLine2 = model.Line2,
            BillingAddressCity = model.City,
            BillingAddressState = model.State,
            BillingAddressPostalCode = model.PostalCode,
            BillingAddressCountry = model.Country,
        };
        await paymentService.SaveTaxInfoAsync(organization, taxInfo);
    }

    /// <summary>
    /// Tries to grant owner access to the Secrets Manager for the organization
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="userId"></param>
    private async Task TryGrantOwnerAccessToSecretsManagerAsync(Guid organizationId, Guid userId)
    {
        var organizationUser = await organizationUserRepository.GetByOrganizationAsync(organizationId, userId);

        if (organizationUser != null)
        {
            organizationUser.AccessSecretsManager = true;
            await organizationUserRepository.ReplaceAsync(organizationUser);
        }
    }

    /// <summary>
    /// Adjusts the organization seats for the Secrets Manager trial to match the new seat count for secrets manager
    /// </summary>
    /// <param name="id"></param>
    /// <param name="organization"></param>
    /// <param name="model"></param>
    private async Task<Organization> AdjustOrganizationSeatsForSmTrialAsync(Guid id, Organization organization,
        SecretsManagerSubscriptionUpdateRequestModel model)
    {
        if (string.IsNullOrWhiteSpace(organization.GatewayCustomerId) ||
            string.IsNullOrWhiteSpace(organization.GatewaySubscriptionId) ||
            model.SeatAdjustment == 0)
        {
            return organization;
        }

        var subscriptionInfo = await paymentService.GetSubscriptionAsync(organization);
        if (subscriptionInfo?.CustomerDiscount?.Id != StripeConstants.CouponIDs.SecretsManagerStandalone)
        {
            return organization;
        }

        await organizationService.UpdateSubscription(id, model.SeatAdjustment, null);

        return await organizationRepository.GetByIdAsync(id);
    }

    private async Task SaveOrganizationInstallationAsync(Guid organizationId, Guid installationId)
    {
        var organizationInstallation =
            await organizationInstallationRepository.GetByInstallationIdAsync(installationId);

        if (organizationInstallation == null)
        {
            await organizationInstallationRepository.CreateAsync(new OrganizationInstallation
            {
                OrganizationId = organizationId,
                InstallationId = installationId
            });
        }
        else if (organizationInstallation.OrganizationId == organizationId)
        {
            organizationInstallation.RevisionDate = DateTime.UtcNow;
            await organizationInstallationRepository.ReplaceAsync(organizationInstallation);
        }
    }

    private async Task<ProfileOrganizationResponseModel> GetProfileOrganizationResponseModelAsync(
        Guid organizationId,
        Guid userId)
    {
        var organizationUserDetails = await organizationUserRepository.GetDetailsByUserAsync(
            userId,
            organizationId,
            OrganizationUserStatusType.Confirmed);

        var organizationIdsManagingActiveUser = (await userService.GetOrganizationsManagingUserAsync(userId))
            .Select(o => o.Id);

        return new ProfileOrganizationResponseModel(organizationUserDetails, organizationIdsManagingActiveUser);
    }
}
