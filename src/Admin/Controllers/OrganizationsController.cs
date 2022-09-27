using Bit.Admin.Models;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Models.OrganizationConnectionConfigs;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Admin.Controllers;

[Authorize]
public class OrganizationsController : Controller
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IOrganizationConnectionRepository _organizationConnectionRepository;
    private readonly ISelfHostedSyncSponsorshipsCommand _syncSponsorshipsCommand;
    private readonly ICipherRepository _cipherRepository;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IGroupRepository _groupRepository;
    private readonly IPolicyRepository _policyRepository;
    private readonly IPaymentService _paymentService;
    private readonly ILicensingService _licensingService;
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly GlobalSettings _globalSettings;
    private readonly IReferenceEventService _referenceEventService;
    private readonly IUserService _userService;
    private readonly ILogger<OrganizationsController> _logger;

    public OrganizationsController(
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationConnectionRepository organizationConnectionRepository,
        ISelfHostedSyncSponsorshipsCommand syncSponsorshipsCommand,
        ICipherRepository cipherRepository,
        ICollectionRepository collectionRepository,
        IGroupRepository groupRepository,
        IPolicyRepository policyRepository,
        IPaymentService paymentService,
        ILicensingService licensingService,
        IApplicationCacheService applicationCacheService,
        GlobalSettings globalSettings,
        IReferenceEventService referenceEventService,
        IUserService userService,
        ILogger<OrganizationsController> logger)
    {
        _organizationRepository = organizationRepository;
        _organizationUserRepository = organizationUserRepository;
        _organizationConnectionRepository = organizationConnectionRepository;
        _syncSponsorshipsCommand = syncSponsorshipsCommand;
        _cipherRepository = cipherRepository;
        _collectionRepository = collectionRepository;
        _groupRepository = groupRepository;
        _policyRepository = policyRepository;
        _paymentService = paymentService;
        _licensingService = licensingService;
        _applicationCacheService = applicationCacheService;
        _globalSettings = globalSettings;
        _referenceEventService = referenceEventService;
        _userService = userService;
        _logger = logger;
    }

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

        var skip = (page - 1) * count;
        var organizations = await _organizationRepository.SearchAsync(name, userEmail, paid, skip, count);
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
        return View(new OrganizationViewModel(organization, billingSyncConnection, users, ciphers, collections, groups, policies));
    }

    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<IActionResult> Edit(Guid id)
    {
        var organization = await _organizationRepository.GetByIdAsync(id);
        if (organization == null)
        {
            return RedirectToAction("Index");
        }

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
        var billingSyncConnection = _globalSettings.EnableCloudCommunication ? await _organizationConnectionRepository.GetByOrganizationIdTypeAsync(id, OrganizationConnectionType.CloudBillingSync) : null;
        return View(new OrganizationEditModel(organization, users, ciphers, collections, groups, policies,
            billingInfo, billingSyncConnection, _globalSettings));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<IActionResult> Edit(Guid id, OrganizationEditModel model)
    {
        var organization = await _organizationRepository.GetByIdAsync(id);
        model.ToOrganization(organization);
        await _organizationRepository.ReplaceAsync(organization);
        await _applicationCacheService.UpsertOrganizationAbilityAsync(organization);
        await _referenceEventService.RaiseEventAsync(new ReferenceEvent(ReferenceEventType.OrganizationEditedByAdmin, organization)
        {
            EventRaisedByUser = _userService.GetUserName(User),
            SalesAssistedTrialStarted = model.SalesAssistedTrialStarted,
        });
        return RedirectToAction("Edit", new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var organization = await _organizationRepository.GetByIdAsync(id);
        if (organization != null)
        {
            await _organizationRepository.DeleteAsync(organization);
            await _applicationCacheService.DeleteOrganizationAbilityAsync(organization.Id);
        }

        return RedirectToAction("Index");
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

}
