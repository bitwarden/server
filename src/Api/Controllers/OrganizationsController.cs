using System.Text.Json;
using Bit.Api.Models.Request;
using Bit.Api.Models.Request.Accounts;
using Bit.Api.Models.Request.Organizations;
using Bit.Api.Models.Response;
using Bit.Api.Models.Response.Organizations;
using Bit.Api.Utilities;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data.Organizations.Policies;
using Bit.Core.OrganizationFeatures.OrganizationApiKeys.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers;

[Route("organizations")]
[Authorize("Application")]
public class OrganizationsController : Controller
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IPolicyRepository _policyRepository;
    private readonly IOrganizationService _organizationService;
    private readonly IUserService _userService;
    private readonly IPaymentService _paymentService;
    private readonly ICurrentContext _currentContext;
    private readonly ISsoConfigRepository _ssoConfigRepository;
    private readonly ISsoConfigService _ssoConfigService;
    private readonly IGetOrganizationApiKeyQuery _getOrganizationApiKeyQuery;
    private readonly IRotateOrganizationApiKeyCommand _rotateOrganizationApiKeyCommand;
    private readonly ICreateOrganizationApiKeyCommand _createOrganizationApiKeyCommand;
    private readonly IOrganizationApiKeyRepository _organizationApiKeyRepository;
    private readonly GlobalSettings _globalSettings;

    public OrganizationsController(
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IPolicyRepository policyRepository,
        IOrganizationService organizationService,
        IUserService userService,
        IPaymentService paymentService,
        ICurrentContext currentContext,
        ISsoConfigRepository ssoConfigRepository,
        ISsoConfigService ssoConfigService,
        IGetOrganizationApiKeyQuery getOrganizationApiKeyQuery,
        IRotateOrganizationApiKeyCommand rotateOrganizationApiKeyCommand,
        ICreateOrganizationApiKeyCommand createOrganizationApiKeyCommand,
        IOrganizationApiKeyRepository organizationApiKeyRepository,
        GlobalSettings globalSettings)
    {
        _organizationRepository = organizationRepository;
        _organizationUserRepository = organizationUserRepository;
        _policyRepository = policyRepository;
        _organizationService = organizationService;
        _userService = userService;
        _paymentService = paymentService;
        _currentContext = currentContext;
        _ssoConfigRepository = ssoConfigRepository;
        _ssoConfigService = ssoConfigService;
        _getOrganizationApiKeyQuery = getOrganizationApiKeyQuery;
        _rotateOrganizationApiKeyCommand = rotateOrganizationApiKeyCommand;
        _createOrganizationApiKeyCommand = createOrganizationApiKeyCommand;
        _organizationApiKeyRepository = organizationApiKeyRepository;
        _globalSettings = globalSettings;
    }

    [HttpGet("{id}")]
    public async Task<OrganizationResponseModel> Get(string id)
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

        return new OrganizationResponseModel(organization);
    }

    [HttpGet("{id}/billing")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<BillingResponseModel> GetBilling(string id)
    {
        var orgIdGuid = new Guid(id);
        if (!await _currentContext.ManageBilling(orgIdGuid))
        {
            throw new NotFoundException();
        }

        var organization = await _organizationRepository.GetByIdAsync(orgIdGuid);
        if (organization == null)
        {
            throw new NotFoundException();
        }

        var billingInfo = await _paymentService.GetBillingAsync(organization);
        return new BillingResponseModel(billingInfo);
    }

    [HttpGet("{id}/subscription")]
    public async Task<OrganizationSubscriptionResponseModel> GetSubscription(string id)
    {
        var orgIdGuid = new Guid(id);
        if (!await _currentContext.ManageBilling(orgIdGuid))
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
            return new OrganizationSubscriptionResponseModel(organization, subscriptionInfo);
        }
        else
        {
            return new OrganizationSubscriptionResponseModel(organization);
        }
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

        var license = await _organizationService.GenerateLicenseAsync(orgIdGuid, installationId);
        if (license == null)
        {
            throw new NotFoundException();
        }

        return license;
    }

    [HttpGet("")]
    public async Task<ListResponseModel<ProfileOrganizationResponseModel>> GetUser()
    {
        var userId = _userService.GetProperUserId(User).Value;
        var organizations = await _organizationUserRepository.GetManyDetailsByUserAsync(userId,
            OrganizationUserStatusType.Confirmed);
        var responses = organizations.Select(o => new ProfileOrganizationResponseModel(o));
        return new ListResponseModel<ProfileOrganizationResponseModel>(responses);
    }

    [HttpGet("{identifier}/auto-enroll-status")]
    public async Task<OrganizationAutoEnrollStatusResponseModel> GetAutoEnrollStatus(string identifier)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        var organization = await _organizationRepository.GetByIdentifierAsync(identifier);
        if (organization == null)
        {
            throw new NotFoundException();
        }

        var organizationUser = await _organizationUserRepository.GetByOrganizationAsync(organization.Id, user.Id);
        if (organizationUser == null)
        {
            throw new NotFoundException();
        }

        var resetPasswordPolicy =
            await _policyRepository.GetByOrganizationIdTypeAsync(organization.Id, PolicyType.ResetPassword);
        if (resetPasswordPolicy == null || !resetPasswordPolicy.Enabled || resetPasswordPolicy.Data == null)
        {
            return new OrganizationAutoEnrollStatusResponseModel(organization.Id, false);
        }

        var data = JsonSerializer.Deserialize<ResetPasswordDataModel>(resetPasswordPolicy.Data, JsonHelpers.IgnoreCase);
        return new OrganizationAutoEnrollStatusResponseModel(organization.Id, data?.AutoEnrollEnabled ?? false);
    }

    [HttpPost("")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<OrganizationResponseModel> Post([FromBody] OrganizationCreateRequestModel model)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        var organizationSignup = model.ToOrganizationSignup(user);
        var result = await _organizationService.SignUpAsync(organizationSignup);
        return new OrganizationResponseModel(result.Item1);
    }

    [HttpPost("license")]
    [SelfHosted(SelfHostedOnly = true)]
    public async Task<OrganizationResponseModel> PostLicense(OrganizationCreateLicenseRequestModel model)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        var license = await ApiHelpers.ReadJsonFileFromBody<OrganizationLicense>(HttpContext, model.License);
        if (license == null)
        {
            throw new BadRequestException("Invalid license");
        }

        var result = await _organizationService.SignUpAsync(license, user, model.Key,
            model.CollectionName, model.Keys?.PublicKey, model.Keys?.EncryptedPrivateKey);
        return new OrganizationResponseModel(result.Item1);
    }

    [HttpPut("{id}")]
    [HttpPost("{id}")]
    public async Task<OrganizationResponseModel> Put(string id, [FromBody] OrganizationUpdateRequestModel model)
    {
        var orgIdGuid = new Guid(id);

        var organization = await _organizationRepository.GetByIdAsync(orgIdGuid);
        if (organization == null)
        {
            throw new NotFoundException();
        }

        var updateBilling = !_globalSettings.SelfHosted && (model.BusinessName != organization.BusinessName ||
            model.BillingEmail != organization.BillingEmail);

        var hasRequiredPermissions = updateBilling
            ? await _currentContext.ManageBilling(orgIdGuid)
            : await _currentContext.OrganizationOwner(orgIdGuid);

        if (!hasRequiredPermissions)
        {
            throw new NotFoundException();
        }

        await _organizationService.UpdateAsync(model.ToOrganization(organization, _globalSettings), updateBilling);
        return new OrganizationResponseModel(organization);
    }

    [HttpPost("{id}/payment")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task PostPayment(string id, [FromBody] PaymentRequestModel model)
    {
        var orgIdGuid = new Guid(id);
        if (!await _currentContext.ManageBilling(orgIdGuid))
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
        if (!await _currentContext.ManageBilling(orgIdGuid))
        {
            throw new NotFoundException();
        }

        var result = await _organizationService.UpgradePlanAsync(orgIdGuid, model.ToOrganizationUpgrade());
        return new PaymentResponseModel
        {
            Success = result.Item1,
            PaymentIntentClientSecret = result.Item2
        };
    }

    [HttpPost("{id}/subscription")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task PostSubscription(string id, [FromBody] OrganizationSubscriptionUpdateRequestModel model)
    {
        var orgIdGuid = new Guid(id);
        if (!await _currentContext.ManageBilling(orgIdGuid))
        {
            throw new NotFoundException();
        }

        await _organizationService.UpdateSubscription(orgIdGuid, model.SeatAdjustment, model.MaxAutoscaleSeats);
    }

    [HttpPost("{id}/seat")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<PaymentResponseModel> PostSeat(string id, [FromBody] OrganizationSeatRequestModel model)
    {
        var orgIdGuid = new Guid(id);
        if (!await _currentContext.ManageBilling(orgIdGuid))
        {
            throw new NotFoundException();
        }

        var result = await _organizationService.AdjustSeatsAsync(orgIdGuid, model.SeatAdjustment.Value);
        return new PaymentResponseModel
        {
            Success = true,
            PaymentIntentClientSecret = result
        };
    }

    [HttpPost("{id}/storage")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<PaymentResponseModel> PostStorage(string id, [FromBody] StorageRequestModel model)
    {
        var orgIdGuid = new Guid(id);
        if (!await _currentContext.ManageBilling(orgIdGuid))
        {
            throw new NotFoundException();
        }

        var result = await _organizationService.AdjustStorageAsync(orgIdGuid, model.StorageGbAdjustment.Value);
        return new PaymentResponseModel
        {
            Success = true,
            PaymentIntentClientSecret = result
        };
    }

    [HttpPost("{id}/verify-bank")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task PostVerifyBank(string id, [FromBody] OrganizationVerifyBankRequestModel model)
    {
        var orgIdGuid = new Guid(id);
        if (!await _currentContext.ManageBilling(orgIdGuid))
        {
            throw new NotFoundException();
        }

        await _organizationService.VerifyBankAsync(orgIdGuid, model.Amount1.Value, model.Amount2.Value);
    }

    [HttpPost("{id}/cancel")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task PostCancel(string id)
    {
        var orgIdGuid = new Guid(id);
        if (!await _currentContext.ManageBilling(orgIdGuid))
        {
            throw new NotFoundException();
        }

        await _organizationService.CancelSubscriptionAsync(orgIdGuid);
    }

    [HttpPost("{id}/reinstate")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task PostReinstate(string id)
    {
        var orgIdGuid = new Guid(id);
        if (!await _currentContext.ManageBilling(orgIdGuid))
        {
            throw new NotFoundException();
        }

        await _organizationService.ReinstateSubscriptionAsync(orgIdGuid);
    }

    [HttpPost("{id}/leave")]
    public async Task Leave(string id)
    {
        var orgGuidId = new Guid(id);
        if (!await _currentContext.OrganizationUser(orgGuidId))
        {
            throw new NotFoundException();
        }

        var user = await _userService.GetUserByPrincipalAsync(User);

        var ssoConfig = await _ssoConfigRepository.GetByOrganizationIdAsync(orgGuidId);
        if (ssoConfig?.GetData()?.KeyConnectorEnabled == true &&
            user.UsesKeyConnector)
        {
            throw new BadRequestException("Your organization's Single Sign-On settings prevent you from leaving.");
        }


        await _organizationService.DeleteUserAsync(orgGuidId, user.Id);
    }

    [HttpDelete("{id}")]
    [HttpPost("{id}/delete")]
    public async Task Delete(string id, [FromBody] SecretVerificationRequestModel model)
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

        var user = await _userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        if (!await _userService.VerifySecretAsync(user, model.Secret))
        {
            await Task.Delay(2000);
            throw new BadRequestException(string.Empty, "User verification failed.");
        }
        else
        {
            await _organizationService.DeleteAsync(organization);
        }
    }

    [HttpPost("{id}/license")]
    [SelfHosted(SelfHostedOnly = true)]
    public async Task PostLicense(string id, LicenseRequestModel model)
    {
        var orgIdGuid = new Guid(id);
        if (!await _currentContext.OrganizationOwner(orgIdGuid))
        {
            throw new NotFoundException();
        }

        var license = await ApiHelpers.ReadJsonFileFromBody<OrganizationLicense>(HttpContext, model.License);
        if (license == null)
        {
            throw new BadRequestException("Invalid license");
        }

        await _organizationService.UpdateLicenseAsync(new Guid(id), license);
    }

    [HttpPost("{id}/import")]
    public async Task Import(string id, [FromBody] ImportOrganizationUsersRequestModel model)
    {
        if (!_globalSettings.SelfHosted && !model.LargeImport &&
            (model.Groups.Count() > 2000 || model.Users.Count(u => !u.Deleted) > 2000))
        {
            throw new BadRequestException("You cannot import this much data at once.");
        }

        var orgIdGuid = new Guid(id);
        if (!await _currentContext.OrganizationAdmin(orgIdGuid))
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User);
        await _organizationService.ImportAsync(
            orgIdGuid,
            userId.Value,
            model.Groups.Select(g => g.ToImportedGroup(orgIdGuid)),
            model.Users.Where(u => !u.Deleted).Select(u => u.ToImportedOrganizationUser()),
            model.Users.Where(u => u.Deleted).Select(u => u.ExternalId),
            model.OverwriteExisting);
    }

    [HttpPost("{id}/api-key")]
    public async Task<ApiKeyResponseModel> ApiKey(string id, [FromBody] OrganizationApiKeyRequestModel model)
    {
        var orgIdGuid = new Guid(id);
        if (!await HasApiKeyAccessAsync(orgIdGuid, model.Type))
        {
            throw new NotFoundException();
        }

        var organization = await _organizationRepository.GetByIdAsync(orgIdGuid);
        if (organization == null)
        {
            throw new NotFoundException();
        }

        if (model.Type == OrganizationApiKeyType.BillingSync || model.Type == OrganizationApiKeyType.Scim)
        {
            // Non-enterprise orgs should not be able to create or view an apikey of billing sync/scim key types
            var plan = StaticStore.GetPlan(organization.PlanType);
            if (plan.Product != ProductType.Enterprise)
            {
                throw new NotFoundException();
            }
        }

        var organizationApiKey = await _getOrganizationApiKeyQuery
                                     .GetOrganizationApiKeyAsync(organization.Id, model.Type) ??
                                 await _createOrganizationApiKeyCommand.CreateAsync(organization.Id, model.Type);

        var user = await _userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        if (model.Type != OrganizationApiKeyType.Scim
            && !await _userService.VerifySecretAsync(user, model.Secret))
        {
            await Task.Delay(2000);
            throw new BadRequestException("MasterPasswordHash", "Invalid password.");
        }
        else
        {
            var response = new ApiKeyResponseModel(organizationApiKey);
            return response;
        }
    }

    [HttpGet("{id}/api-key-information/{type?}")]
    public async Task<ListResponseModel<OrganizationApiKeyInformation>> ApiKeyInformation(Guid id, [FromRoute] OrganizationApiKeyType? type)
    {
        if (!await HasApiKeyAccessAsync(id, type))
        {
            throw new NotFoundException();
        }

        var apiKeys = await _organizationApiKeyRepository.GetManyByOrganizationIdTypeAsync(id, type);

        return new ListResponseModel<OrganizationApiKeyInformation>(
            apiKeys.Select(k => new OrganizationApiKeyInformation(k)));
    }

    [HttpPost("{id}/rotate-api-key")]
    public async Task<ApiKeyResponseModel> RotateApiKey(string id, [FromBody] OrganizationApiKeyRequestModel model)
    {
        var orgIdGuid = new Guid(id);
        if (!await HasApiKeyAccessAsync(orgIdGuid, model.Type))
        {
            throw new NotFoundException();
        }

        var organization = await _organizationRepository.GetByIdAsync(orgIdGuid);
        if (organization == null)
        {
            throw new NotFoundException();
        }

        var organizationApiKey = await _getOrganizationApiKeyQuery
                                    .GetOrganizationApiKeyAsync(organization.Id, model.Type) ??
                                await _createOrganizationApiKeyCommand.CreateAsync(organization.Id, model.Type);

        var user = await _userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        if (model.Type != OrganizationApiKeyType.Scim
            && !await _userService.VerifySecretAsync(user, model.Secret))
        {
            await Task.Delay(2000);
            throw new BadRequestException("MasterPasswordHash", "Invalid password.");
        }
        else
        {
            await _rotateOrganizationApiKeyCommand.RotateApiKeyAsync(organizationApiKey);
            var response = new ApiKeyResponseModel(organizationApiKey);
            return response;
        }
    }

    private async Task<bool> HasApiKeyAccessAsync(Guid orgId, OrganizationApiKeyType? type)
    {
        return type switch
        {
            OrganizationApiKeyType.Scim => await _currentContext.ManageScim(orgId),
            _ => await _currentContext.OrganizationOwner(orgId),
        };
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
    public async Task PutTaxInfo(string id, [FromBody] OrganizationTaxInfoUpdateRequestModel model)
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

    [HttpGet("{id}/keys")]
    public async Task<OrganizationKeysResponseModel> GetKeys(string id)
    {
        var org = await _organizationRepository.GetByIdAsync(new Guid(id));
        if (org == null)
        {
            throw new NotFoundException();
        }

        return new OrganizationKeysResponseModel(org);
    }

    [HttpPost("{id}/keys")]
    public async Task<OrganizationKeysResponseModel> PostKeys(string id, [FromBody] OrganizationKeysRequestModel model)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        var org = await _organizationService.UpdateOrganizationKeysAsync(new Guid(id), model.PublicKey, model.EncryptedPrivateKey);
        return new OrganizationKeysResponseModel(org);
    }

    [HttpGet("{id:guid}/sso")]
    public async Task<OrganizationSsoResponseModel> GetSso(Guid id)
    {
        if (!await _currentContext.ManageSso(id))
        {
            throw new NotFoundException();
        }

        var organization = await _organizationRepository.GetByIdAsync(id);
        if (organization == null)
        {
            throw new NotFoundException();
        }

        var ssoConfig = await _ssoConfigRepository.GetByOrganizationIdAsync(id);

        return new OrganizationSsoResponseModel(organization, _globalSettings, ssoConfig);
    }

    [HttpPost("{id:guid}/sso")]
    public async Task<OrganizationSsoResponseModel> PostSso(Guid id, [FromBody] OrganizationSsoRequestModel model)
    {
        if (!await _currentContext.ManageSso(id))
        {
            throw new NotFoundException();
        }

        var organization = await _organizationRepository.GetByIdAsync(id);
        if (organization == null)
        {
            throw new NotFoundException();
        }

        var ssoConfig = await _ssoConfigRepository.GetByOrganizationIdAsync(id);
        ssoConfig = ssoConfig == null ? model.ToSsoConfig(id) : model.ToSsoConfig(ssoConfig);
        organization.Identifier = model.Identifier;

        await _ssoConfigService.SaveAsync(ssoConfig, organization);
        await _organizationService.UpdateAsync(organization);

        return new OrganizationSsoResponseModel(organization, _globalSettings, ssoConfig);
    }
}
