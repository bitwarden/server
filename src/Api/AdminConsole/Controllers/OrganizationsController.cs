using System.Text.Json;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.AdminConsole.Models.Response;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Api.Auth.Models.Request.Organizations;
using Bit.Api.Auth.Models.Response.Organizations;
using Bit.Api.Models.Request.Accounts;
using Bit.Api.Models.Request.Organizations;
using Bit.Api.Models.Response;
using Bit.Core;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Business.Tokenables;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationApiKeys.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Repositories;
using Bit.Core.Auth.Services;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Services;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tokens;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.AdminConsole.Controllers;

[Route("organizations")]
[Authorize("Application")]
public class OrganizationsController : Controller
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IPolicyRepository _policyRepository;
    private readonly IOrganizationService _organizationService;
    private readonly IUserService _userService;
    private readonly ICurrentContext _currentContext;
    private readonly ISsoConfigRepository _ssoConfigRepository;
    private readonly ISsoConfigService _ssoConfigService;
    private readonly IGetOrganizationApiKeyQuery _getOrganizationApiKeyQuery;
    private readonly IRotateOrganizationApiKeyCommand _rotateOrganizationApiKeyCommand;
    private readonly ICreateOrganizationApiKeyCommand _createOrganizationApiKeyCommand;
    private readonly IOrganizationApiKeyRepository _organizationApiKeyRepository;
    private readonly IFeatureService _featureService;
    private readonly GlobalSettings _globalSettings;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IProviderRepository _providerRepository;
    private readonly IProviderBillingService _providerBillingService;
    private readonly IDataProtectorTokenFactory<OrgDeleteTokenable> _orgDeleteTokenDataFactory;
    private readonly IRemoveOrganizationUserCommand _removeOrganizationUserCommand;

    public OrganizationsController(
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IPolicyRepository policyRepository,
        IOrganizationService organizationService,
        IUserService userService,
        ICurrentContext currentContext,
        ISsoConfigRepository ssoConfigRepository,
        ISsoConfigService ssoConfigService,
        IGetOrganizationApiKeyQuery getOrganizationApiKeyQuery,
        IRotateOrganizationApiKeyCommand rotateOrganizationApiKeyCommand,
        ICreateOrganizationApiKeyCommand createOrganizationApiKeyCommand,
        IOrganizationApiKeyRepository organizationApiKeyRepository,
        IFeatureService featureService,
        GlobalSettings globalSettings,
        IPushNotificationService pushNotificationService,
        IProviderRepository providerRepository,
        IProviderBillingService providerBillingService,
        IDataProtectorTokenFactory<OrgDeleteTokenable> orgDeleteTokenDataFactory,
        IRemoveOrganizationUserCommand removeOrganizationUserCommand)
    {
        _organizationRepository = organizationRepository;
        _organizationUserRepository = organizationUserRepository;
        _policyRepository = policyRepository;
        _organizationService = organizationService;
        _userService = userService;
        _currentContext = currentContext;
        _ssoConfigRepository = ssoConfigRepository;
        _ssoConfigService = ssoConfigService;
        _getOrganizationApiKeyQuery = getOrganizationApiKeyQuery;
        _rotateOrganizationApiKeyCommand = rotateOrganizationApiKeyCommand;
        _createOrganizationApiKeyCommand = createOrganizationApiKeyCommand;
        _organizationApiKeyRepository = organizationApiKeyRepository;
        _featureService = featureService;
        _globalSettings = globalSettings;
        _pushNotificationService = pushNotificationService;
        _providerRepository = providerRepository;
        _providerBillingService = providerBillingService;
        _orgDeleteTokenDataFactory = orgDeleteTokenDataFactory;
        _removeOrganizationUserCommand = removeOrganizationUserCommand;
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

    [HttpGet("")]
    public async Task<ListResponseModel<ProfileOrganizationResponseModel>> GetUser()
    {
        var userId = _userService.GetProperUserId(User).Value;
        var organizations = await _organizationUserRepository.GetManyDetailsByUserAsync(userId,
            OrganizationUserStatusType.Confirmed);

        var organizationManagingActiveUser = await _userService.GetOrganizationsManagingUserAsync(userId);
        var organizationIdsManagingActiveUser = organizationManagingActiveUser.Select(o => o.Id);

        var responses = organizations.Select(o => new ProfileOrganizationResponseModel(o, organizationIdsManagingActiveUser));
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

    [HttpPost("create-without-payment")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<OrganizationResponseModel> CreateWithoutPaymentAsync([FromBody] OrganizationNoPaymentCreateRequest model)
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

        var updateBilling = !_globalSettings.SelfHosted && (model.BusinessName != organization.DisplayBusinessName() ||
                                                            model.BillingEmail != organization.BillingEmail);

        var hasRequiredPermissions = updateBilling
            ? await _currentContext.EditSubscription(orgIdGuid)
            : await _currentContext.OrganizationOwner(orgIdGuid);

        if (!hasRequiredPermissions)
        {
            throw new NotFoundException();
        }

        await _organizationService.UpdateAsync(model.ToOrganization(organization, _globalSettings), updateBilling);
        return new OrganizationResponseModel(organization);
    }

    [HttpPost("{id}/storage")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<PaymentResponseModel> PostStorage(string id, [FromBody] StorageRequestModel model)
    {
        var orgIdGuid = new Guid(id);
        if (!await _currentContext.EditSubscription(orgIdGuid))
        {
            throw new NotFoundException();
        }

        var result = await _organizationService.AdjustStorageAsync(orgIdGuid, model.StorageGbAdjustment.Value);
        return new PaymentResponseModel { Success = true, PaymentIntentClientSecret = result };
    }

    [HttpPost("{id}/leave")]
    public async Task Leave(Guid id)
    {
        if (!await _currentContext.OrganizationUser(id))
        {
            throw new NotFoundException();
        }

        var user = await _userService.GetUserByPrincipalAsync(User);

        var ssoConfig = await _ssoConfigRepository.GetByOrganizationIdAsync(id);
        if (ssoConfig?.GetData()?.MemberDecryptionType == MemberDecryptionType.KeyConnector && user.UsesKeyConnector)
        {
            throw new BadRequestException("Your organization's Single Sign-On settings prevent you from leaving.");
        }

        if (_featureService.IsEnabled(FeatureFlagKeys.AccountDeprovisioning)
            && (await _userService.GetOrganizationsManagingUserAsync(user.Id)).Any(x => x.Id == id))
        {
            throw new BadRequestException("Managed user account cannot leave managing organization. Contact your organization administrator for additional details.");
        }

        await _removeOrganizationUserCommand.RemoveUserAsync(id, user.Id);
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

        await _organizationService.DeleteAsync(organization);
    }

    [HttpPost("{id}/delete-recover-token")]
    [AllowAnonymous]
    public async Task PostDeleteRecoverToken(Guid id, [FromBody] OrganizationVerifyDeleteRecoverRequestModel model)
    {
        var organization = await _organizationRepository.GetByIdAsync(id);
        if (organization == null)
        {
            throw new NotFoundException();
        }

        if (!_orgDeleteTokenDataFactory.TryUnprotect(model.Token, out var data) || !data.IsValid(organization))
        {
            throw new BadRequestException("Invalid token.");
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

        await _organizationService.DeleteAsync(organization);
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
            if (plan.ProductTier is not ProductTierType.Enterprise and not ProductTierType.Teams)
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
    public async Task<ListResponseModel<OrganizationApiKeyInformation>> ApiKeyInformation(Guid id,
        [FromRoute] OrganizationApiKeyType? type)
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

    [HttpGet("{id}/public-key")]
    public async Task<OrganizationPublicKeyResponseModel> GetPublicKey(string id)
    {
        var org = await _organizationRepository.GetByIdAsync(new Guid(id));
        if (org == null)
        {
            throw new NotFoundException();
        }

        return new OrganizationPublicKeyResponseModel(org);
    }

    [Obsolete("TDL-136 Renamed to public-key (2023.8), left for backwards compatibility with older clients.")]
    [HttpGet("{id}/keys")]
    public async Task<OrganizationPublicKeyResponseModel> GetKeys(string id)
    {
        return await GetPublicKey(id);
    }

    [HttpPost("{id}/keys")]
    public async Task<OrganizationKeysResponseModel> PostKeys(string id, [FromBody] OrganizationKeysRequestModel model)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        var org = await _organizationService.UpdateOrganizationKeysAsync(new Guid(id), model.PublicKey,
            model.EncryptedPrivateKey);
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

    [HttpPut("{id}/collection-management")]
    public async Task<OrganizationResponseModel> PutCollectionManagement(Guid id, [FromBody] OrganizationCollectionManagementUpdateRequestModel model)
    {
        if (
          _globalSettings.SelfHosted &&
          !_featureService.IsEnabled(FeatureFlagKeys.LimitCollectionCreationDeletionSplit)
        )
        {
            throw new BadRequestException("Only allowed when not self hosted.");
        }

        var organization = await _organizationRepository.GetByIdAsync(id);
        if (organization == null)
        {
            throw new NotFoundException();
        }

        if (!await _currentContext.OrganizationOwner(id))
        {
            throw new NotFoundException();
        }

        await _organizationService.UpdateAsync(model.ToOrganization(organization, _featureService), eventType: EventType.Organization_CollectionManagement_Updated);
        return new OrganizationResponseModel(organization);
    }
}
