using System.Net;
using Bit.Admin.AdminConsole.Models;
using Bit.Admin.Enums;
using Bit.Admin.Services;
using Bit.Admin.Utilities;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Providers.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Services;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.OrganizationConnectionConfigs;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;
using Bit.Core.Utilities;
using Bit.Core.Vault.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Admin.AdminConsole.Controllers;

[Authorize]
public class OrganizationsController : Controller
{
    private readonly IOrganizationService _organizationService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IOrganizationConnectionRepository _organizationConnectionRepository;
    private readonly ISelfHostedSyncSponsorshipsCommand _syncSponsorshipsCommand;
    private readonly ICipherRepository _cipherRepository;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IGroupRepository _groupRepository;
    private readonly IPolicyRepository _policyRepository;
    private readonly IPaymentService _paymentService;
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly GlobalSettings _globalSettings;
    private readonly IReferenceEventService _referenceEventService;
    private readonly IUserService _userService;
    private readonly IProviderRepository _providerRepository;
    private readonly ILogger<OrganizationsController> _logger;
    private readonly IAccessControlService _accessControlService;
    private readonly ICurrentContext _currentContext;
    private readonly ISecretRepository _secretRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IServiceAccountRepository _serviceAccountRepository;
    private readonly IProviderOrganizationRepository _providerOrganizationRepository;
    private readonly IRemoveOrganizationFromProviderCommand _removeOrganizationFromProviderCommand;
    private readonly IProviderBillingService _providerBillingService;
    private readonly IFeatureService _featureService;

    public OrganizationsController(
        IOrganizationService organizationService,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationConnectionRepository organizationConnectionRepository,
        ISelfHostedSyncSponsorshipsCommand syncSponsorshipsCommand,
        ICipherRepository cipherRepository,
        ICollectionRepository collectionRepository,
        IGroupRepository groupRepository,
        IPolicyRepository policyRepository,
        IPaymentService paymentService,
        IApplicationCacheService applicationCacheService,
        GlobalSettings globalSettings,
        IReferenceEventService referenceEventService,
        IUserService userService,
        IProviderRepository providerRepository,
        ILogger<OrganizationsController> logger,
        IAccessControlService accessControlService,
        ICurrentContext currentContext,
        ISecretRepository secretRepository,
        IProjectRepository projectRepository,
        IServiceAccountRepository serviceAccountRepository,
        IProviderOrganizationRepository providerOrganizationRepository,
        IRemoveOrganizationFromProviderCommand removeOrganizationFromProviderCommand,
        IProviderBillingService providerBillingService,
        IFeatureService featureService)
    {
        _organizationService = organizationService;
        _organizationRepository = organizationRepository;
        _organizationUserRepository = organizationUserRepository;
        _organizationConnectionRepository = organizationConnectionRepository;
        _syncSponsorshipsCommand = syncSponsorshipsCommand;
        _cipherRepository = cipherRepository;
        _collectionRepository = collectionRepository;
        _groupRepository = groupRepository;
        _policyRepository = policyRepository;
        _paymentService = paymentService;
        _applicationCacheService = applicationCacheService;
        _globalSettings = globalSettings;
        _referenceEventService = referenceEventService;
        _userService = userService;
        _providerRepository = providerRepository;
        _logger = logger;
        _accessControlService = accessControlService;
        _currentContext = currentContext;
        _secretRepository = secretRepository;
        _projectRepository = projectRepository;
        _serviceAccountRepository = serviceAccountRepository;
        _providerOrganizationRepository = providerOrganizationRepository;
        _removeOrganizationFromProviderCommand = removeOrganizationFromProviderCommand;
        _providerBillingService = providerBillingService;
        _featureService = featureService;
    }

    [RequirePermission(Permission.Org_List_View)]
    public async Task<IActionResult> Index(string name = null, string userEmail = null, bool? paid = null,
        int page = 1, int count = 25)
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
        var organizations = await _organizationRepository.SearchAsync(encodedName, userEmail, paid, skip, count);
        return View(new OrganizationsModel
        {
            Items = organizations as List<Organization>,
            Name = string.IsNullOrWhiteSpace(name) ? null : name,
            UserEmail = string.IsNullOrWhiteSpace(userEmail) ? null : userEmail,
            Paid = paid,
            Page = page,
            Count = count,
            Action = _globalSettings.SelfHosted ? "View" : "Edit",
            SelfHosted = _globalSettings.SelfHosted
        });
    }

    public async Task<IActionResult> View(Guid id)
    {
        var organization = await _organizationRepository.GetByIdAsync(id);
        if (organization == null)
        {
            return RedirectToAction("Index");
        }

        var provider = await _providerRepository.GetByOrganizationIdAsync(id);
        var ciphers = await _cipherRepository.GetManyByOrganizationIdAsync(id);
        var collections = await _collectionRepository.GetManyByOrganizationIdAsync(id);
        IEnumerable<Group> groups = null;
        if (organization.UseGroups)
        {
            groups = await _groupRepository.GetManyByOrganizationIdAsync(id);
        }
        IEnumerable<Policy> policies = null;
        if (organization.UsePolicies)
        {
            policies = await _policyRepository.GetManyByOrganizationIdAsync(id);
        }
        var users = await _organizationUserRepository.GetManyDetailsByOrganizationAsync(id);
        var billingSyncConnection = _globalSettings.EnableCloudCommunication ? await _organizationConnectionRepository.GetByOrganizationIdTypeAsync(id, OrganizationConnectionType.CloudBillingSync) : null;
        var secrets = organization.UseSecretsManager ? await _secretRepository.GetSecretsCountByOrganizationIdAsync(id) : -1;
        var projects = organization.UseSecretsManager ? await _projectRepository.GetProjectCountByOrganizationIdAsync(id) : -1;
        var serviceAccounts = organization.UseSecretsManager ? await _serviceAccountRepository.GetServiceAccountCountByOrganizationIdAsync(id) : -1;
        var smSeats = organization.UseSecretsManager
            ? await _organizationUserRepository.GetOccupiedSmSeatCountByOrganizationIdAsync(organization.Id)
            : -1;
        return View(new OrganizationViewModel(organization, provider, billingSyncConnection, users, ciphers, collections, groups, policies,
            secrets, projects, serviceAccounts, smSeats));
    }

    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<IActionResult> Edit(Guid id)
    {
        var organization = await _organizationRepository.GetByIdAsync(id);
        if (organization == null)
        {
            return RedirectToAction("Index");
        }

        var provider = await _providerRepository.GetByOrganizationIdAsync(id);
        var ciphers = await _cipherRepository.GetManyByOrganizationIdAsync(id);
        var collections = await _collectionRepository.GetManyByOrganizationIdAsync(id);
        IEnumerable<Group> groups = null;
        if (organization.UseGroups)
        {
            groups = await _groupRepository.GetManyByOrganizationIdAsync(id);
        }
        IEnumerable<Policy> policies = null;
        if (organization.UsePolicies)
        {
            policies = await _policyRepository.GetManyByOrganizationIdAsync(id);
        }
        var users = await _organizationUserRepository.GetManyDetailsByOrganizationAsync(id);
        var billingInfo = await _paymentService.GetBillingAsync(organization);
        var billingHistoryInfo = await _paymentService.GetBillingHistoryAsync(organization);
        var billingSyncConnection = _globalSettings.EnableCloudCommunication ? await _organizationConnectionRepository.GetByOrganizationIdTypeAsync(id, OrganizationConnectionType.CloudBillingSync) : null;
        var secrets = organization.UseSecretsManager ? await _secretRepository.GetSecretsCountByOrganizationIdAsync(id) : -1;
        var projects = organization.UseSecretsManager ? await _projectRepository.GetProjectCountByOrganizationIdAsync(id) : -1;
        var serviceAccounts = organization.UseSecretsManager ? await _serviceAccountRepository.GetServiceAccountCountByOrganizationIdAsync(id) : -1;

        var smSeats = organization.UseSecretsManager
            ? await _organizationUserRepository.GetOccupiedSmSeatCountByOrganizationIdAsync(organization.Id)
            : -1;

        return View(new OrganizationEditModel(
            organization,
            provider,
            users,
            ciphers,
            collections,
            groups,
            policies,
            billingInfo,
            billingHistoryInfo,
            billingSyncConnection,
            _globalSettings,
            secrets,
            projects,
            serviceAccounts,
            smSeats));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<IActionResult> Edit(Guid id, OrganizationEditModel model)
    {
        var organization = await _organizationRepository.GetByIdAsync(id);

        if (organization == null)
        {
            TempData["Error"] = "Could not find organization to update.";
            return RedirectToAction("Index");
        }

        var existingOrganizationData = new Organization
        {
            Id = organization.Id,
            Status = organization.Status,
            PlanType = organization.PlanType,
            Seats = organization.Seats
        };

        UpdateOrganization(organization, model);

        if (organization.UseSecretsManager &&
            !StaticStore.GetPlan(organization.PlanType).SupportsSecretsManager)
        {
            TempData["Error"] = "Plan does not support Secrets Manager";
            return RedirectToAction("Edit", new { id });
        }

        await HandlePotentialProviderSeatScalingAsync(
            existingOrganizationData,
            model);

        await _organizationRepository.ReplaceAsync(organization);

        await _applicationCacheService.UpsertOrganizationAbilityAsync(organization);
        await _referenceEventService.RaiseEventAsync(new ReferenceEvent(ReferenceEventType.OrganizationEditedByAdmin, organization, _currentContext)
        {
            EventRaisedByUser = _userService.GetUserName(User),
            SalesAssistedTrialStarted = model.SalesAssistedTrialStarted,
        });

        return RedirectToAction("Edit", new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permission.Org_Delete)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var organization = await _organizationRepository.GetByIdAsync(id);

        if (organization == null)
        {
            return RedirectToAction("Index");
        }

        if (organization.IsValidClient())
        {
            var provider = await _providerRepository.GetByOrganizationIdAsync(organization.Id);

            if (provider.IsBillable())
            {
                await _providerBillingService.ScaleSeats(
                    provider,
                    organization.PlanType,
                    -organization.Seats ?? 0);
            }
        }

        await _organizationRepository.DeleteAsync(organization);
        await _applicationCacheService.DeleteOrganizationAbilityAsync(organization.Id);

        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permission.Org_Delete)]
    public async Task<IActionResult> DeleteInitiation(Guid id, OrganizationInitiateDeleteModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = ModelState.GetErrorMessage();
        }
        else
        {
            try
            {
                var organization = await _organizationRepository.GetByIdAsync(id);
                if (organization != null)
                {
                    await _organizationService.InitiateDeleteAsync(organization, model.AdminEmail);
                    TempData["Success"] = "The request to initiate deletion of the organization has been sent.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }
        }

        return RedirectToAction("Edit", new { id });
    }

    public async Task<IActionResult> TriggerBillingSync(Guid id)
    {
        var organization = await _organizationRepository.GetByIdAsync(id);
        if (organization == null)
        {
            return RedirectToAction("Index");
        }
        var connection = (await _organizationConnectionRepository.GetEnabledByOrganizationIdTypeAsync(id, OrganizationConnectionType.CloudBillingSync)).FirstOrDefault();
        if (connection != null)
        {
            try
            {
                var config = connection.GetConfig<BillingSyncConfig>();
                await _syncSponsorshipsCommand.SyncOrganization(id, config.CloudOrganizationId, connection);
                TempData["ConnectionActivated"] = id;
                TempData["ConnectionError"] = null;
            }
            catch (Exception ex)
            {
                TempData["ConnectionError"] = ex.Message;
                _logger.LogWarning(ex, "Error while attempting to do billing sync for organization with id '{OrganizationId}'", id);
            }

            if (_globalSettings.SelfHosted)
            {
                return RedirectToAction("View", new { id });
            }
            else
            {
                return RedirectToAction("Edit", new { id });
            }
        }
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> ResendOwnerInvite(Guid id)
    {
        var organization = await _organizationRepository.GetByIdAsync(id);
        if (organization == null)
        {
            return RedirectToAction("Index");
        }

        var organizationUsers = await _organizationUserRepository.GetManyByOrganizationAsync(id, OrganizationUserType.Owner);
        foreach (var organizationUser in organizationUsers)
        {
            await _organizationService.ResendInviteAsync(id, null, organizationUser.Id, true);
        }

        return Json(null);
    }

    [HttpPost]
    [RequirePermission(Permission.Provider_Edit)]
    public async Task<IActionResult> UnlinkOrganizationFromProviderAsync(Guid id)
    {
        var organization = await _organizationRepository.GetByIdAsync(id);
        if (organization is null)
        {
            return RedirectToAction("Index");
        }

        var provider = await _providerRepository.GetByOrganizationIdAsync(id);
        if (provider is null)
        {
            return RedirectToAction("Edit", new { id });
        }

        var providerOrganization = await _providerOrganizationRepository.GetByOrganizationId(id);
        if (providerOrganization is null)
        {
            return RedirectToAction("Edit", new { id });
        }

        await _removeOrganizationFromProviderCommand.RemoveOrganizationFromProvider(
            provider,
            providerOrganization,
            organization);

        return Json(null);
    }

    private void UpdateOrganization(Organization organization, OrganizationEditModel model)
    {
        if (_accessControlService.UserHasPermission(Permission.Org_CheckEnabledBox))
        {
            organization.Enabled = model.Enabled;
        }

        if (_accessControlService.UserHasPermission(Permission.Org_Plan_Edit))
        {
            organization.PlanType = model.PlanType.Value;
            organization.Plan = model.Plan;
            organization.Seats = model.Seats;
            organization.MaxAutoscaleSeats = model.MaxAutoscaleSeats;
            organization.MaxCollections = model.MaxCollections;
            organization.MaxStorageGb = model.MaxStorageGb;

            //features
            organization.SelfHost = model.SelfHost;
            organization.Use2fa = model.Use2fa;
            organization.UseApi = model.UseApi;
            organization.UseGroups = model.UseGroups;
            organization.UsePolicies = model.UsePolicies;
            organization.UseSso = model.UseSso;
            organization.UseKeyConnector = model.UseKeyConnector;
            organization.UseScim = model.UseScim;
            organization.UseDirectory = model.UseDirectory;
            organization.UseEvents = model.UseEvents;
            organization.UseResetPassword = model.UseResetPassword;
            organization.UseCustomPermissions = model.UseCustomPermissions;
            organization.UseTotp = model.UseTotp;
            organization.UsersGetPremium = model.UsersGetPremium;
            organization.UseSecretsManager = model.UseSecretsManager;

            //secrets
            organization.SmSeats = model.SmSeats;
            organization.MaxAutoscaleSmSeats = model.MaxAutoscaleSmSeats;
            organization.SmServiceAccounts = model.SmServiceAccounts;
            organization.MaxAutoscaleSmServiceAccounts = model.MaxAutoscaleSmServiceAccounts;
        }

        if (_accessControlService.UserHasPermission(Permission.Org_Licensing_Edit))
        {
            organization.LicenseKey = model.LicenseKey;
            organization.ExpirationDate = model.ExpirationDate;
        }

        if (_accessControlService.UserHasPermission(Permission.Org_Billing_Edit))
        {
            organization.BillingEmail = model.BillingEmail?.ToLowerInvariant()?.Trim();
            organization.Gateway = model.Gateway;
            organization.GatewayCustomerId = model.GatewayCustomerId;
            organization.GatewaySubscriptionId = model.GatewaySubscriptionId;
        }
    }

    private async Task HandlePotentialProviderSeatScalingAsync(
        Organization organization,
        OrganizationEditModel update)
    {
        var scaleMSPOnClientOrganizationUpdate =
            _featureService.IsEnabled(FeatureFlagKeys.PM14401_ScaleMSPOnClientOrganizationUpdate);

        if (!scaleMSPOnClientOrganizationUpdate)
        {
            return;
        }

        var provider = await _providerRepository.GetByOrganizationIdAsync(organization.Id);

        // No scaling required
        if (provider is not { Type: ProviderType.Msp, Status: ProviderStatusType.Billable } ||
            organization is not { Status: OrganizationStatusType.Managed } ||
            !organization.Seats.HasValue ||
            update is { Seats: null, PlanType: null } ||
            update is { PlanType: not PlanType.TeamsMonthly and not PlanType.EnterpriseMonthly } ||
            (PlanTypesMatch() && SeatsMatch()))
        {
            return;
        }

        // Only scale the plan
        if (!PlanTypesMatch() && SeatsMatch())
        {
            await _providerBillingService.ScaleSeats(provider, organization.PlanType, -organization.Seats.Value);
            await _providerBillingService.ScaleSeats(provider, update.PlanType!.Value, organization.Seats.Value);
        }
        // Only scale the seats
        else if (PlanTypesMatch() && !SeatsMatch())
        {
            var seatAdjustment = update.Seats!.Value - organization.Seats.Value;
            await _providerBillingService.ScaleSeats(provider, organization.PlanType, seatAdjustment);
        }
        // Scale both
        else if (!PlanTypesMatch() && !SeatsMatch())
        {
            var seatAdjustment = update.Seats!.Value - organization.Seats.Value;
            var planTypeAdjustment = organization.Seats.Value;
            var totalAdjustment = seatAdjustment + planTypeAdjustment;

            await _providerBillingService.ScaleSeats(provider, organization.PlanType, -organization.Seats.Value);
            await _providerBillingService.ScaleSeats(provider, update.PlanType!.Value, totalAdjustment);
        }

        return;

        bool PlanTypesMatch()
            => update.PlanType.HasValue && update.PlanType.Value == organization.PlanType;

        bool SeatsMatch()
            => update.Seats.HasValue && update.Seats.Value == organization.Seats;
    }
}
