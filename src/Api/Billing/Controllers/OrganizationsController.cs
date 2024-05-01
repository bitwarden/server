using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.AdminConsole.Models.Response;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Api.Models.Request;
using Bit.Api.Models.Request.Organizations;
using Bit.Api.Models.Response;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Queries;
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
public class OrganizationsController : Controller
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IOrganizationService _organizationService;
    private readonly IUserService _userService;
    private readonly IPaymentService _paymentService;
    private readonly ICurrentContext _currentContext;
    private readonly ICloudGetOrganizationLicenseQuery _cloudGetOrganizationLicenseQuery;
    private readonly GlobalSettings _globalSettings;
    private readonly ILicensingService _licensingService;
    private readonly IUpdateSecretsManagerSubscriptionCommand _updateSecretsManagerSubscriptionCommand;
    private readonly IUpgradeOrganizationPlanCommand _upgradeOrganizationPlanCommand;
    private readonly IAddSecretsManagerSubscriptionCommand _addSecretsManagerSubscriptionCommand;
    private readonly ICancelSubscriptionCommand _cancelSubscriptionCommand;
    private readonly ISubscriberQueries _subscriberQueries;
    private readonly IReferenceEventService _referenceEventService;

    public OrganizationsController(
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
        ICancelSubscriptionCommand cancelSubscriptionCommand,
        ISubscriberQueries subscriberQueries,
        IReferenceEventService referenceEventService)
    {
        _organizationRepository = organizationRepository;
        _organizationUserRepository = organizationUserRepository;
        _organizationService = organizationService;
        _userService = userService;
        _paymentService = paymentService;
        _currentContext = currentContext;
        _cloudGetOrganizationLicenseQuery = cloudGetOrganizationLicenseQuery;
        _globalSettings = globalSettings;
        _licensingService = licensingService;
        _updateSecretsManagerSubscriptionCommand = updateSecretsManagerSubscriptionCommand;
        _upgradeOrganizationPlanCommand = upgradeOrganizationPlanCommand;
        _addSecretsManagerSubscriptionCommand = addSecretsManagerSubscriptionCommand;
        _cancelSubscriptionCommand = cancelSubscriptionCommand;
        _subscriberQueries = subscriberQueries;
        _referenceEventService = referenceEventService;
    }

    [HttpGet("{id}/billing-status")]
    public async Task<OrganizationBillingStatusResponseModel> GetBillingStatus(Guid id)
    {
        if (!await _currentContext.EditPaymentMethods(id))
        {
            throw new NotFoundException();
        }

        var organization = await _organizationRepository.GetByIdAsync(id);

        var risksSubscriptionFailure = await _paymentService.RisksSubscriptionFailure(organization);

        return new OrganizationBillingStatusResponseModel(organization, risksSubscriptionFailure);
    }

    [HttpGet("{id}/subscription")]
    public async Task<OrganizationSubscriptionResponseModel> GetSubscription(string id)
    {
        var orgIdGuid = new Guid(id);
        if (!await _currentContext.ViewSubscription(orgIdGuid))
        {
            throw new NotFoundException();
        }

        var organization = await _organizationRepository.GetByIdAsync(orgIdGuid);
        if (organization == null)
        {
            throw new NotFoundException();
        }

        if (!_globalSettings.SelfHosted && organization.Gateway != null)
        {
            var subscriptionInfo = await _paymentService.GetSubscriptionAsync(organization);
            if (subscriptionInfo == null)
            {
                throw new NotFoundException();
            }

            var hideSensitiveData = !await _currentContext.EditSubscription(orgIdGuid);

            return new OrganizationSubscriptionResponseModel(organization, subscriptionInfo, hideSensitiveData);
        }

        if (_globalSettings.SelfHosted)
        {
            var orgLicense = await _licensingService.ReadOrganizationLicenseAsync(organization);
            return new OrganizationSubscriptionResponseModel(organization, orgLicense);
        }

        return new OrganizationSubscriptionResponseModel(organization);
    }

    [HttpGet("{id}/license")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<OrganizationLicense> GetLicense(string id, [FromQuery] Guid installationId)
    {
        var orgIdGuid = new Guid(id);
        if (!await _currentContext.OrganizationOwner(orgIdGuid))
        {
            throw new NotFoundException();
        }

        var org = await _organizationRepository.GetByIdAsync(new Guid(id));
        var license = await _cloudGetOrganizationLicenseQuery.GetLicenseAsync(org, installationId);
        if (license == null)
        {
            throw new NotFoundException();
        }

        return license;
    }

    [HttpPost("{id}/payment")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task PostPayment(string id, [FromBody] PaymentRequestModel model)
    {
        var orgIdGuid = new Guid(id);
        if (!await _currentContext.EditPaymentMethods(orgIdGuid))
        {
            throw new NotFoundException();
        }

        await _organizationService.ReplacePaymentMethodAsync(orgIdGuid, model.PaymentToken,
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

    [HttpPost("{id}/upgrade")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<PaymentResponseModel> PostUpgrade(string id, [FromBody] OrganizationUpgradeRequestModel model)
    {
        var orgIdGuid = new Guid(id);
        if (!await _currentContext.EditSubscription(orgIdGuid))
        {
            throw new NotFoundException();
        }

        var (success, paymentIntentClientSecret) = await _upgradeOrganizationPlanCommand.UpgradePlanAsync(orgIdGuid, model.ToOrganizationUpgrade());

        if (model.UseSecretsManager && success)
        {
            var userId = _userService.GetProperUserId(User).Value;

            await TryGrantOwnerAccessToSecretsManagerAsync(orgIdGuid, userId);
        }

        return new PaymentResponseModel { Success = success, PaymentIntentClientSecret = paymentIntentClientSecret };
    }

    [HttpPost("{id}/sm-subscription")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task PostSmSubscription(Guid id, [FromBody] SecretsManagerSubscriptionUpdateRequestModel model)
    {
        var organization = await _organizationRepository.GetByIdAsync(id);
        if (organization == null)
        {
            throw new NotFoundException();
        }

        if (!await _currentContext.EditSubscription(id))
        {
            throw new NotFoundException();
        }

        organization = await AdjustOrganizationSeatsForSmTrialAsync(id, organization, model);

        var organizationUpdate = model.ToSecretsManagerSubscriptionUpdate(organization);

        await _updateSecretsManagerSubscriptionCommand.UpdateSubscriptionAsync(organizationUpdate);
    }

    [HttpPost("{id}/subscription")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task PostSubscription(string id, [FromBody] OrganizationSubscriptionUpdateRequestModel model)
    {
        var orgIdGuid = new Guid(id);
        if (!await _currentContext.EditSubscription(orgIdGuid))
        {
            throw new NotFoundException();
        }
        await _organizationService.UpdateSubscription(orgIdGuid, model.SeatAdjustment, model.MaxAutoscaleSeats);
    }

    [HttpPost("{id}/subscribe-secrets-manager")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<ProfileOrganizationResponseModel> PostSubscribeSecretsManagerAsync(Guid id, [FromBody] SecretsManagerSubscribeRequestModel model)
    {
        var organization = await _organizationRepository.GetByIdAsync(id);
        if (organization == null)
        {
            throw new NotFoundException();
        }

        if (!await _currentContext.EditSubscription(id))
        {
            throw new NotFoundException();
        }

        await _addSecretsManagerSubscriptionCommand.SignUpAsync(organization, model.AdditionalSmSeats,
            model.AdditionalServiceAccounts);

        var userId = _userService.GetProperUserId(User).Value;

        await TryGrantOwnerAccessToSecretsManagerAsync(organization.Id, userId);

        var organizationDetails = await _organizationUserRepository.GetDetailsByUserAsync(userId, organization.Id,
            OrganizationUserStatusType.Confirmed);

        return new ProfileOrganizationResponseModel(organizationDetails);
    }

    [HttpPost("{id}/seat")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<PaymentResponseModel> PostSeat(string id, [FromBody] OrganizationSeatRequestModel model)
    {
        var orgIdGuid = new Guid(id);
        if (!await _currentContext.EditSubscription(orgIdGuid))
        {
            throw new NotFoundException();
        }

        var result = await _organizationService.AdjustSeatsAsync(orgIdGuid, model.SeatAdjustment.Value);
        return new PaymentResponseModel { Success = true, PaymentIntentClientSecret = result };
    }

    [HttpPost("{id}/verify-bank")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task PostVerifyBank(string id, [FromBody] OrganizationVerifyBankRequestModel model)
    {
        var orgIdGuid = new Guid(id);
        if (!await _currentContext.EditSubscription(orgIdGuid))
        {
            throw new NotFoundException();
        }

        await _organizationService.VerifyBankAsync(orgIdGuid, model.Amount1.Value, model.Amount2.Value);
    }

    [HttpPost("{id}/cancel")]
    public async Task PostCancel(Guid id, [FromBody] SubscriptionCancellationRequestModel request)
    {
        if (!await _currentContext.EditSubscription(id))
        {
            throw new NotFoundException();
        }

        var organization = await _organizationRepository.GetByIdAsync(id);

        if (organization == null)
        {
            throw new NotFoundException();
        }

        var subscription = await _subscriberQueries.GetSubscriptionOrThrow(organization);

        await _cancelSubscriptionCommand.CancelSubscription(subscription,
            new OffboardingSurveyResponse
            {
                UserId = _currentContext.UserId!.Value,
                Reason = request.Reason,
                Feedback = request.Feedback
            },
            organization.IsExpired());

        await _referenceEventService.RaiseEventAsync(new ReferenceEvent(
            ReferenceEventType.CancelSubscription,
            organization,
            _currentContext)
        {
            EndOfPeriod = organization.IsExpired()
        });
    }

    [HttpPost("{id}/reinstate")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task PostReinstate(string id)
    {
        var orgIdGuid = new Guid(id);
        if (!await _currentContext.EditSubscription(orgIdGuid))
        {
            throw new NotFoundException();
        }

        await _organizationService.ReinstateSubscriptionAsync(orgIdGuid);
    }

    [HttpGet("{id}/tax")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<TaxInfoResponseModel> GetTaxInfo(string id)
    {
        var orgIdGuid = new Guid(id);
        if (!await _currentContext.OrganizationOwner(orgIdGuid))
        {
            throw new NotFoundException();
        }

        var organization = await _organizationRepository.GetByIdAsync(orgIdGuid);
        if (organization == null)
        {
            throw new NotFoundException();
        }

        var taxInfo = await _paymentService.GetTaxInfoAsync(organization);
        return new TaxInfoResponseModel(taxInfo);
    }

    [HttpPut("{id}/tax")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task PutTaxInfo(string id, [FromBody] ExpandedTaxInfoUpdateRequestModel model)
    {
        var orgIdGuid = new Guid(id);
        if (!await _currentContext.OrganizationOwner(orgIdGuid))
        {
            throw new NotFoundException();
        }

        var organization = await _organizationRepository.GetByIdAsync(orgIdGuid);
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
        await _paymentService.SaveTaxInfoAsync(organization, taxInfo);
    }

    /// <summary>
    /// Tries to grant owner access to the Secrets Manager for the organization
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="userId"></param>
    private async Task TryGrantOwnerAccessToSecretsManagerAsync(Guid organizationId, Guid userId)
    {
        var organizationUser = await _organizationUserRepository.GetByOrganizationAsync(organizationId, userId);

        if (organizationUser != null)
        {
            organizationUser.AccessSecretsManager = true;
            await _organizationUserRepository.ReplaceAsync(organizationUser);
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

        var subscriptionInfo = await _paymentService.GetSubscriptionAsync(organization);
        if (subscriptionInfo?.CustomerDiscount?.Id != "sm-standalone")
        {
            return organization;
        }

        await _organizationService.UpdateSubscription(id, model.SeatAdjustment, null);

        return await _organizationRepository.GetByIdAsync(id);
    }
}
