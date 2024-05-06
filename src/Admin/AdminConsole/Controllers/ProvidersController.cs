using System.ComponentModel.DataAnnotations;
using System.Net;
using Bit.Admin.AdminConsole.Models;
using Bit.Admin.Enums;
using Bit.Admin.Utilities;
using Bit.Core;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Providers.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Billing.Entities;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Repositories;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tools.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Admin.AdminConsole.Controllers;

[Authorize]
[SelfHosted(NotSelfHostedOnly = true)]
public class ProvidersController : Controller
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationService _organizationService;
    private readonly IProviderRepository _providerRepository;
    private readonly IProviderUserRepository _providerUserRepository;
    private readonly IProviderOrganizationRepository _providerOrganizationRepository;
    private readonly GlobalSettings _globalSettings;
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly IProviderService _providerService;
    private readonly IReferenceEventService _referenceEventService;
    private readonly IUserService _userService;
    private readonly ICreateProviderCommand _createProviderCommand;
    private readonly IFeatureService _featureService;
    private readonly IProviderPlanRepository _providerPlanRepository;

    public ProvidersController(
        IOrganizationRepository organizationRepository,
        IOrganizationService organizationService,
        IProviderRepository providerRepository,
        IProviderUserRepository providerUserRepository,
        IProviderOrganizationRepository providerOrganizationRepository,
        IProviderService providerService,
        GlobalSettings globalSettings,
        IApplicationCacheService applicationCacheService,
        IReferenceEventService referenceEventService,
        IUserService userService,
        ICreateProviderCommand createProviderCommand,
        IFeatureService featureService,
        IProviderPlanRepository providerPlanRepository)
    {
        _organizationRepository = organizationRepository;
        _organizationService = organizationService;
        _providerRepository = providerRepository;
        _providerUserRepository = providerUserRepository;
        _providerOrganizationRepository = providerOrganizationRepository;
        _providerService = providerService;
        _globalSettings = globalSettings;
        _applicationCacheService = applicationCacheService;
        _referenceEventService = referenceEventService;
        _userService = userService;
        _createProviderCommand = createProviderCommand;
        _featureService = featureService;
        _providerPlanRepository = providerPlanRepository;
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

    public IActionResult Create(int teamsMinimumSeats, int enterpriseMinimumSeats, string ownerEmail = null)
    {
        return View(new CreateProviderModel
        {
            OwnerEmail = ownerEmail,
            TeamsMinimumSeats = teamsMinimumSeats,
            EnterpriseMinimumSeats = enterpriseMinimumSeats
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permission.Provider_Create)]
    public async Task<IActionResult> Create(CreateProviderModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var provider = model.ToProvider();
        switch (provider.Type)
        {
            case ProviderType.Msp:
                await _createProviderCommand.CreateMspAsync(provider, model.OwnerEmail, model.TeamsMinimumSeats,
                    model.EnterpriseMinimumSeats);
                break;
            case ProviderType.Reseller:
                await _createProviderCommand.CreateResellerAsync(provider);
                break;
        }

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
        return View(new ProviderViewModel(provider, users, providerOrganizations));
    }

    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<IActionResult> Edit(Guid id)
    {
        var provider = await _providerRepository.GetByIdAsync(id);
        if (provider == null)
        {
            return RedirectToAction("Index");
        }

        var users = await _providerUserRepository.GetManyDetailsByProviderAsync(id);
        var providerOrganizations = await _providerOrganizationRepository.GetManyDetailsByProviderAsync(id);

        var isConsolidatedBillingEnabled = _featureService.IsEnabled(FeatureFlagKeys.EnableConsolidatedBilling);

        if (!isConsolidatedBillingEnabled || !provider.IsBillable())
        {
            return View(new ProviderEditModel(provider, users, providerOrganizations, new List<ProviderPlan>()));
        }

        var providerPlan = await _providerPlanRepository.GetByProviderId(id);
        return View(new ProviderEditModel(provider, users, providerOrganizations, providerPlan));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [SelfHosted(NotSelfHostedOnly = true)]
    [RequirePermission(Permission.Provider_Edit)]
    public async Task<IActionResult> Edit(Guid id, ProviderEditModel model)
    {
        var providerPlans = await _providerPlanRepository.GetByProviderId(id);
        var provider = await _providerRepository.GetByIdAsync(id);
        if (provider == null)
        {
            return RedirectToAction("Index");
        }

        model.ToProvider(provider);
        await _providerRepository.ReplaceAsync(provider);
        await _applicationCacheService.UpsertProviderAbilityAsync(provider);

        var isConsolidatedBillingEnabled = _featureService.IsEnabled(FeatureFlagKeys.EnableConsolidatedBilling);

        if (!isConsolidatedBillingEnabled || !provider.IsBillable())
        {
            return RedirectToAction("Edit", new { id });
        }

        model.ToProviderPlan(providerPlans);
        if (providerPlans.Count == 0)
        {
            var newProviderPlans = new List<ProviderPlan>
            {
                new() {ProviderId = provider.Id, PlanType = PlanType.TeamsMonthly, SeatMinimum= model.TeamsMinimumSeats, PurchasedSeats = 0, AllocatedSeats = 0},
                new() {ProviderId = provider.Id, PlanType = PlanType.EnterpriseMonthly, SeatMinimum= model.EnterpriseMinimumSeats, PurchasedSeats = 0, AllocatedSeats = 0}
            };

            foreach (var newProviderPlan in newProviderPlans)
            {
                await _providerPlanRepository.CreateAsync(newProviderPlan);
            }
        }
        else
        {
            foreach (var providerPlan in providerPlans)
            {
                await _providerPlanRepository.ReplaceAsync(providerPlan);
            }
        }

        return RedirectToAction("Edit", new { id });
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

        return View(new OrganizationEditModel(provider));
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrganization(Guid providerId, OrganizationEditModel model)
    {
        var provider = await _providerRepository.GetByIdAsync(providerId);
        if (provider is not { Type: ProviderType.Reseller })
        {
            return RedirectToAction("Index");
        }

        var flexibleCollectionsSignupEnabled = _featureService.IsEnabled(FeatureFlagKeys.FlexibleCollectionsSignup);
        var flexibleCollectionsV1Enabled = _featureService.IsEnabled(FeatureFlagKeys.FlexibleCollectionsV1);
        var organization = model.CreateOrganization(provider, flexibleCollectionsSignupEnabled, flexibleCollectionsV1Enabled);
        await _organizationService.CreatePendingOrganization(organization, model.Owners, User, _userService, model.SalesAssistedTrialStarted);
        await _providerService.AddOrganization(providerId, organization.Id, null);

        return RedirectToAction("Edit", "Providers", new { id = providerId });
    }

    [HttpPost]
    [SelfHosted(NotSelfHostedOnly = true)]
    [RequirePermission(Permission.Provider_Edit)]
    public async Task<IActionResult> Delete(Guid id, string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            return BadRequest("Invalid provider name");
        }

        var providerOrganizations = await _providerOrganizationRepository.GetManyDetailsByProviderAsync(id);

        if (providerOrganizations.Count > 0)
        {
            return BadRequest("You must unlink all clients before you can delete a provider");
        }

        var provider = await _providerRepository.GetByIdAsync(id);

        if (provider is null)
        {
            return BadRequest("Provider does not exist");
        }

        if (!string.Equals(providerName.Trim(), provider.Name, StringComparison.OrdinalIgnoreCase))
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
}
