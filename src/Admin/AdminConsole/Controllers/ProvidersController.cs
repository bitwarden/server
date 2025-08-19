// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Net;
using Bit.Admin.AdminConsole.Models;
using Bit.Admin.Enums;
using Bit.Admin.Services;
using Bit.Admin.Utilities;
using Bit.Core;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations;
using Bit.Core.AdminConsole.Providers.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Providers.Entities;
using Bit.Core.Billing.Providers.Models;
using Bit.Core.Billing.Providers.Repositories;
using Bit.Core.Billing.Providers.Services;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe;

namespace Bit.Admin.AdminConsole.Controllers;

[Authorize]
[SelfHosted(NotSelfHostedOnly = true)]
public class ProvidersController : Controller
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IResellerClientOrganizationSignUpCommand _resellerClientOrganizationSignUpCommand;
    private readonly IProviderRepository _providerRepository;
    private readonly IProviderUserRepository _providerUserRepository;
    private readonly IProviderOrganizationRepository _providerOrganizationRepository;
    private readonly GlobalSettings _globalSettings;
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly IProviderService _providerService;
    private readonly ICreateProviderCommand _createProviderCommand;
    private readonly IFeatureService _featureService;
    private readonly IProviderPlanRepository _providerPlanRepository;
    private readonly IProviderBillingService _providerBillingService;
    private readonly IPricingClient _pricingClient;
    private readonly IStripeAdapter _stripeAdapter;
    private readonly IAccessControlService _accessControlService;
    private readonly string _stripeUrl;
    private readonly string _braintreeMerchantUrl;
    private readonly string _braintreeMerchantId;

    public ProvidersController(
        IOrganizationRepository organizationRepository,
        IResellerClientOrganizationSignUpCommand resellerClientOrganizationSignUpCommand,
        IProviderRepository providerRepository,
        IProviderUserRepository providerUserRepository,
        IProviderOrganizationRepository providerOrganizationRepository,
        IProviderService providerService,
        GlobalSettings globalSettings,
        IApplicationCacheService applicationCacheService,
        ICreateProviderCommand createProviderCommand,
        IFeatureService featureService,
        IProviderPlanRepository providerPlanRepository,
        IProviderBillingService providerBillingService,
        IWebHostEnvironment webHostEnvironment,
        IPricingClient pricingClient,
        IStripeAdapter stripeAdapter,
        IAccessControlService accessControlService)
    {
        _organizationRepository = organizationRepository;
        _resellerClientOrganizationSignUpCommand = resellerClientOrganizationSignUpCommand;
        _providerRepository = providerRepository;
        _providerUserRepository = providerUserRepository;
        _providerOrganizationRepository = providerOrganizationRepository;
        _providerService = providerService;
        _globalSettings = globalSettings;
        _applicationCacheService = applicationCacheService;
        _createProviderCommand = createProviderCommand;
        _featureService = featureService;
        _providerPlanRepository = providerPlanRepository;
        _providerBillingService = providerBillingService;
        _pricingClient = pricingClient;
        _stripeAdapter = stripeAdapter;
        _stripeUrl = webHostEnvironment.GetStripeUrl();
        _braintreeMerchantUrl = webHostEnvironment.GetBraintreeMerchantUrl();
        _braintreeMerchantId = globalSettings.Braintree.MerchantId;
        _accessControlService = accessControlService;
    }

    [RequirePermission(Permission.Provider_List_View)]
    public async Task<IActionResult> Index(string name = null, string userEmail = null, int page = 1, int count = 25)
    {
        if (page < 1)
        {
            page = 1;
        }

        if (count < 1)
        {
            count = 1;
        }

        var skip = (page - 1) * count;
        var providers = await _providerRepository.SearchAsync(name, userEmail, skip, count);
        return View(new ProvidersModel
        {
            Items = providers as List<Provider>,
            Name = string.IsNullOrWhiteSpace(name) ? null : name,
            UserEmail = string.IsNullOrWhiteSpace(userEmail) ? null : userEmail,
            Page = page,
            Count = count,
            Action = _globalSettings.SelfHosted ? "View" : "Edit",
            SelfHosted = _globalSettings.SelfHosted
        });
    }

    public IActionResult Create()
    {
        return View(new CreateProviderModel());
    }

    [HttpGet("providers/create/msp")]
    public IActionResult CreateMsp(int teamsMinimumSeats, int enterpriseMinimumSeats, string ownerEmail = null)
    {
        return View(new CreateMspProviderModel
        {
            OwnerEmail = ownerEmail,
            TeamsMonthlySeatMinimum = teamsMinimumSeats,
            EnterpriseMonthlySeatMinimum = enterpriseMinimumSeats
        });
    }

    [HttpGet("providers/create/reseller")]
    public IActionResult CreateReseller()
    {
        return View(new CreateResellerProviderModel());
    }

    [HttpGet("providers/create/business-unit")]
    public IActionResult CreateBusinessUnit(int enterpriseMinimumSeats, string ownerEmail = null)
    {
        return View(new CreateBusinessUnitProviderModel
        {
            OwnerEmail = ownerEmail,
            EnterpriseSeatMinimum = enterpriseMinimumSeats
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permission.Provider_Create)]
    public IActionResult Create(CreateProviderModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        return model.Type switch
        {
            ProviderType.Msp => RedirectToAction("CreateMsp"),
            ProviderType.Reseller => RedirectToAction("CreateReseller"),
            ProviderType.BusinessUnit => RedirectToAction("CreateBusinessUnit"),
            _ => View(model)
        };
    }

    [HttpPost("providers/create/msp")]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permission.Provider_Create)]
    public async Task<IActionResult> CreateMsp(CreateMspProviderModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var provider = model.ToProvider();

        await _createProviderCommand.CreateMspAsync(
            provider,
            model.OwnerEmail,
            model.TeamsMonthlySeatMinimum,
            model.EnterpriseMonthlySeatMinimum);

        return RedirectToAction("Edit", new { id = provider.Id });
    }

    [HttpPost("providers/create/reseller")]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permission.Provider_Create)]
    public async Task<IActionResult> CreateReseller(CreateResellerProviderModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }
        var provider = model.ToProvider();
        await _createProviderCommand.CreateResellerAsync(provider);

        return RedirectToAction("Edit", new { id = provider.Id });
    }

    [HttpPost("providers/create/business-unit")]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permission.Provider_Create)]
    public async Task<IActionResult> CreateBusinessUnit(CreateBusinessUnitProviderModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }
        var provider = model.ToProvider();

        await _createProviderCommand.CreateBusinessUnitAsync(
            provider,
            model.OwnerEmail,
            model.Plan.Value,
            model.EnterpriseSeatMinimum);

        return RedirectToAction("Edit", new { id = provider.Id });
    }

    [RequirePermission(Permission.Provider_View)]
    public async Task<IActionResult> View(Guid id)
    {
        var provider = await _providerRepository.GetByIdAsync(id);
        if (provider == null)
        {
            return RedirectToAction("Index");
        }

        var users = await _providerUserRepository.GetManyDetailsByProviderAsync(id);
        var providerOrganizations = await _providerOrganizationRepository.GetManyDetailsByProviderAsync(id);
        var providerPlans = await _providerPlanRepository.GetByProviderId(id);
        return View(new ProviderViewModel(provider, users, providerOrganizations, providerPlans.ToList()));
    }

    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<IActionResult> Edit(Guid id)
    {
        var provider = await GetEditModel(id);
        if (provider == null)
        {
            return RedirectToAction("Index");
        }

        return View(provider);
    }

    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var provider = await GetEditModel(id);
        if (provider == null)
        {
            return RedirectToAction("Index");
        }

        return RedirectToAction("Edit", new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [SelfHosted(NotSelfHostedOnly = true)]
    [RequirePermission(Permission.Provider_Edit)]
    public async Task<IActionResult> Edit(Guid id, ProviderEditModel model)
    {
        var provider = await _providerRepository.GetByIdAsync(id);

        if (provider == null)
        {
            return RedirectToAction("Index");
        }

        if (provider.Type != model.Type)
        {
            var oldModel = await GetEditModel(id);
            ModelState.AddModelError(nameof(model.Type), "Provider type cannot be changed.");
            return View(oldModel);
        }

        if (!ModelState.IsValid)
        {
            var oldModel = await GetEditModel(id);
            ModelState[nameof(ProviderEditModel.BillingEmail)]!.RawValue = oldModel.BillingEmail;
            return View(oldModel);
        }

        // validate the stripe ids to prevent saving a bad one
        if (provider.IsBillable())
        {
            if (!string.IsNullOrEmpty(model.GatewayCustomerId) &&
                !await _providerBillingService.IsValidGatewayCustomerIdAsync(model.GatewayCustomerId))
            {
                var oldModel = await GetEditModel(id);
                ModelState.AddModelError(nameof(model.GatewayCustomerId), $"Invalid Gateway Customer Id: {model.GatewayCustomerId}");
                return View(oldModel);
            }
            if (!string.IsNullOrEmpty(model.GatewayCustomerId) &&
                !await _providerBillingService.IsValidGatewaySubscriptionIdAsync(model.GatewaySubscriptionId))
            {
                var oldModel = await GetEditModel(id);
                ModelState.AddModelError(nameof(model.GatewaySubscriptionId), $"Invalid Gateway Subscription Id: {model.GatewaySubscriptionId}");
                return View(oldModel);
            }
        }

        var originalProviderStatus = provider.Enabled;

        model.ToProvider(provider);

        provider.Enabled = _accessControlService.UserHasPermission(Permission.Provider_CheckEnabledBox)
            ? model.Enabled : originalProviderStatus;

        await _providerService.UpdateAsync(provider);
        await _applicationCacheService.UpsertProviderAbilityAsync(provider);

        if (!provider.IsBillable())
        {
            return RedirectToAction("Edit", new { id });
        }

        var providerPlans = await _providerPlanRepository.GetByProviderId(id);

        switch (provider.Type)
        {
            case ProviderType.Msp:
                var updateMspSeatMinimumsCommand = new UpdateProviderSeatMinimumsCommand(
                    provider,
                    [
                        (Plan: PlanType.TeamsMonthly, SeatsMinimum: model.TeamsMonthlySeatMinimum),
                        (Plan: PlanType.EnterpriseMonthly, SeatsMinimum: model.EnterpriseMonthlySeatMinimum)
                    ]);
                await _providerBillingService.UpdateSeatMinimums(updateMspSeatMinimumsCommand);

                if (_featureService.IsEnabled(FeatureFlagKeys.PM199566_UpdateMSPToChargeAutomatically))
                {
                    var customer = await _stripeAdapter.CustomerGetAsync(provider.GatewayCustomerId);

                    if (model.PayByInvoice != customer.ApprovedToPayByInvoice())
                    {
                        var approvedToPayByInvoice = model.PayByInvoice ? "1" : "0";
                        await _stripeAdapter.CustomerUpdateAsync(customer.Id, new CustomerUpdateOptions
                        {
                            Metadata = new Dictionary<string, string>
                            {
                                [StripeConstants.MetadataKeys.InvoiceApproved] = approvedToPayByInvoice
                            }
                        });
                    }
                }
                break;
            case ProviderType.BusinessUnit:
                {
                    var existingMoePlan = providerPlans.Single();

                    // 1. Change the plan and take over any old values.
                    var changeMoePlanCommand = new ChangeProviderPlanCommand(
                        provider,
                        existingMoePlan.Id,
                        model.Plan!.Value);
                    await _providerBillingService.ChangePlan(changeMoePlanCommand);

                    // 2. Update the seat minimums.
                    var updateMoeSeatMinimumsCommand = new UpdateProviderSeatMinimumsCommand(
                        provider,
                        [
                            (Plan: model.Plan!.Value, SeatsMinimum: model.EnterpriseMinimumSeats!.Value)
                        ]);
                    await _providerBillingService.UpdateSeatMinimums(updateMoeSeatMinimumsCommand);
                    break;
                }
        }

        return RedirectToAction("Edit", new { id });
    }

    private async Task<ProviderEditModel> GetEditModel(Guid id)
    {
        var provider = await _providerRepository.GetByIdAsync(id);
        if (provider == null)
        {
            return null;
        }

        var users = await _providerUserRepository.GetManyDetailsByProviderAsync(id);
        var providerOrganizations = await _providerOrganizationRepository.GetManyDetailsByProviderAsync(id);

        if (!provider.IsBillable())
        {
            return new ProviderEditModel(provider, users, providerOrganizations, new List<ProviderPlan>(), false);
        }

        var providerPlans = await _providerPlanRepository.GetByProviderId(id);
        var payByInvoice = false;
        // prevent the page from crashing if stripe errors
        try
        {
            payByInvoice =
                _featureService.IsEnabled(FeatureFlagKeys.PM199566_UpdateMSPToChargeAutomatically) &&
                (await _stripeAdapter.CustomerGetAsync(provider.GatewayCustomerId)).ApprovedToPayByInvoice();
        }
        catch (StripeException e)
        {
            TempData["Error"] = $"Stripe Error: {e.Message}";
        }

        return new ProviderEditModel(
            provider, users, providerOrganizations,
            providerPlans.ToList(), payByInvoice, GetGatewayCustomerUrl(provider), GetGatewaySubscriptionUrl(provider));
    }

    [RequirePermission(Permission.Provider_ResendEmailInvite)]
    public async Task<IActionResult> ResendInvite(Guid ownerId, Guid providerId)
    {
        await _providerService.ResendProviderSetupInviteEmailAsync(providerId, ownerId);
        TempData["InviteResentTo"] = ownerId;
        return RedirectToAction("Edit", new { id = providerId });
    }

    [HttpGet]
    public async Task<IActionResult> AddExistingOrganization(Guid id, string name = null, string ownerEmail = null, int page = 1, int count = 25)
    {
        if (page < 1)
        {
            page = 1;
        }

        if (count < 1)
        {
            count = 1;
        }

        var encodedName = WebUtility.HtmlEncode(name);
        var skip = (page - 1) * count;
        var unassignedOrganizations = await _organizationRepository.SearchUnassignedToProviderAsync(encodedName, ownerEmail, skip, count);
        var viewModel = new OrganizationUnassignedToProviderSearchViewModel
        {
            OrganizationName = string.IsNullOrWhiteSpace(name) ? null : name,
            OrganizationOwnerEmail = string.IsNullOrWhiteSpace(ownerEmail) ? null : ownerEmail,
            Page = page,
            Count = count,
            Items = unassignedOrganizations.Select(uo => new OrganizationSelectableViewModel
            {
                Id = uo.Id,
                Name = uo.DisplayName(),
                PlanType = uo.PlanType
            }).ToList()
        };

        return View(viewModel);
    }

    [HttpPost]
    public async Task<IActionResult> AddExistingOrganization(Guid id, OrganizationUnassignedToProviderSearchViewModel model)
    {
        var organizationIds = model.Items.Where(o => o.Selected).Select(o => o.Id).ToArray();
        if (organizationIds.Any())
        {
            await _providerService.AddOrganizationsToReseller(id, organizationIds);
        }

        return RedirectToAction("Edit", "Providers", new { id = id });
    }

    [HttpGet]
    public async Task<IActionResult> CreateOrganization(Guid providerId)
    {
        var provider = await _providerRepository.GetByIdAsync(providerId);
        if (provider is not { Type: ProviderType.Reseller })
        {
            return RedirectToAction("Index");
        }

        var plans = await _pricingClient.ListPlans();

        return View(new OrganizationEditModel(provider, plans));
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrganization(Guid providerId, OrganizationEditModel model)
    {
        var provider = await _providerRepository.GetByIdAsync(providerId);
        if (provider is not { Type: ProviderType.Reseller })
        {
            return RedirectToAction("Index");
        }

        var organization = model.CreateOrganization(provider);
        await _resellerClientOrganizationSignUpCommand.SignUpResellerClientAsync(organization, model.Owners);
        await _providerService.AddOrganization(providerId, organization.Id, null);

        return RedirectToAction("Edit", "Providers", new { id = providerId });
    }

    [HttpPost]
    [SelfHosted(NotSelfHostedOnly = true)]
    [RequirePermission(Permission.Provider_Edit)]
    public async Task<IActionResult> Delete(Guid id, string providerName)
    {
        var provider = await _providerRepository.GetByIdAsync(id);

        if (provider is null)
        {
            return BadRequest("Provider does not exist");
        }

        if (provider.Status == ProviderStatusType.Pending)
        {
            await _providerService.DeleteAsync(provider);
            return NoContent();
        }

        if (string.IsNullOrWhiteSpace(providerName))
        {
            return BadRequest("Invalid provider name");
        }

        var providerOrganizations = await _providerOrganizationRepository.GetManyDetailsByProviderAsync(id);

        if (providerOrganizations.Count > 0)
        {
            return BadRequest("You must unlink all clients before you can delete a provider");
        }

        if (!string.Equals(providerName.Trim(), provider.DisplayName(), StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Invalid provider name");
        }

        await _providerService.DeleteAsync(provider);
        return NoContent();
    }

    [HttpPost]
    [SelfHosted(NotSelfHostedOnly = true)]
    [RequirePermission(Permission.Provider_Edit)]
    public async Task<IActionResult> DeleteInitiation(Guid id, string providerEmail)
    {
        var emailAttribute = new EmailAddressAttribute();
        if (!emailAttribute.IsValid(providerEmail))
        {
            return BadRequest("Invalid provider admin email");
        }

        var provider = await _providerRepository.GetByIdAsync(id);
        if (provider != null)
        {
            try
            {
                await _providerService.InitiateDeleteAsync(provider, providerEmail);
            }
            catch (BadRequestException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        return NoContent();
    }

    private string GetGatewayCustomerUrl(Provider provider)
    {
        if (!provider.Gateway.HasValue || string.IsNullOrEmpty(provider.GatewayCustomerId))
        {
            return null;
        }

        return provider.Gateway switch
        {
            GatewayType.Stripe => $"{_stripeUrl}/customers/{provider.GatewayCustomerId}",
            GatewayType.PayPal => $"{_braintreeMerchantUrl}/{_braintreeMerchantId}/${provider.GatewayCustomerId}",
            _ => null
        };
    }

    private string GetGatewaySubscriptionUrl(Provider provider)
    {
        if (!provider.Gateway.HasValue || string.IsNullOrEmpty(provider.GatewaySubscriptionId))
        {
            return null;
        }

        return provider.Gateway switch
        {
            GatewayType.Stripe => $"{_stripeUrl}/subscriptions/{provider.GatewaySubscriptionId}",
            GatewayType.PayPal => $"{_braintreeMerchantUrl}/{_braintreeMerchantId}/subscriptions/${provider.GatewaySubscriptionId}",
            _ => null
        };
    }
}
