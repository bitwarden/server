using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Repositories;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;

namespace Bit.Core.AdminConsole.Services.Implementations;

public class PolicyService : IPolicyService
{
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly IEventService _eventService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IPolicyRepository _policyRepository;
    private readonly ISsoConfigRepository _ssoConfigRepository;
    private readonly IMailService _mailService;
    private readonly GlobalSettings _globalSettings;
    private readonly ITwoFactorIsEnabledQuery _twoFactorIsEnabledQuery;
    private readonly IFeatureService _featureService;
    private readonly ISavePolicyCommand _savePolicyCommand;
    private readonly IRemoveOrganizationUserCommand _removeOrganizationUserCommand;
    private readonly IOrganizationHasVerifiedDomainsQuery _organizationHasVerifiedDomainsQuery;

    public PolicyService(
        IApplicationCacheService applicationCacheService,
        IEventService eventService,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IPolicyRepository policyRepository,
        ISsoConfigRepository ssoConfigRepository,
        IMailService mailService,
        GlobalSettings globalSettings,
        ITwoFactorIsEnabledQuery twoFactorIsEnabledQuery,
        IFeatureService featureService,
        ISavePolicyCommand savePolicyCommand,
        IRemoveOrganizationUserCommand removeOrganizationUserCommand,
        IOrganizationHasVerifiedDomainsQuery organizationHasVerifiedDomainsQuery)
    {
        _applicationCacheService = applicationCacheService;
        _eventService = eventService;
        _organizationRepository = organizationRepository;
        _organizationUserRepository = organizationUserRepository;
        _policyRepository = policyRepository;
        _ssoConfigRepository = ssoConfigRepository;
        _mailService = mailService;
        _globalSettings = globalSettings;
        _twoFactorIsEnabledQuery = twoFactorIsEnabledQuery;
        _featureService = featureService;
        _savePolicyCommand = savePolicyCommand;
        _removeOrganizationUserCommand = removeOrganizationUserCommand;
        _organizationHasVerifiedDomainsQuery = organizationHasVerifiedDomainsQuery;
    }

    public async Task SaveAsync(Policy policy, Guid? savingUserId)
    {
        if (_featureService.IsEnabled(FeatureFlagKeys.Pm13322AddPolicyDefinitions))
        {
            // Transitional mapping - this will be moved to callers once the feature flag is removed
            var policyUpdate = new PolicyUpdate
            {
                OrganizationId = policy.OrganizationId,
                Type = policy.Type,
                Enabled = policy.Enabled,
                Data = policy.Data
            };

            await _savePolicyCommand.SaveAsync(policyUpdate);
            return;
        }

        var org = await _organizationRepository.GetByIdAsync(policy.OrganizationId);
        if (org == null)
        {
            throw new BadRequestException("Organization not found");
        }

        if (!org.UsePolicies)
        {
            throw new BadRequestException("This organization cannot use policies.");
        }

        // FIXME: This method will throw a bunch of errors based on if the
        // policy that is being applied requires some other policy that is
        // not enabled. It may be advisable to refactor this into a domain
        // object and get this kind of stuff out of the service.
        await HandleDependentPoliciesAsync(policy, org);

        var now = DateTime.UtcNow;
        if (policy.Id == default(Guid))
        {
            policy.CreationDate = now;
        }

        policy.RevisionDate = now;

        // We can exit early for disable operations, because they are
        // simpler.
        if (!policy.Enabled)
        {
            await SetPolicyConfiguration(policy);
            return;
        }

        await EnablePolicyAsync(policy, org, savingUserId);
    }

    public async Task<MasterPasswordPolicyData> GetMasterPasswordPolicyForUserAsync(User user)
    {
        var policies = (await _policyRepository.GetManyByUserIdAsync(user.Id))
            .Where(p => p.Type == PolicyType.MasterPassword && p.Enabled)
            .ToList();

        if (!policies.Any())
        {
            return null;
        }

        var enforcedOptions = new MasterPasswordPolicyData();

        foreach (var policy in policies)
        {
            enforcedOptions.CombineWith(policy.GetDataModel<MasterPasswordPolicyData>());
        }

        return enforcedOptions;
    }

    public async Task<ICollection<OrganizationUserPolicyDetails>> GetPoliciesApplicableToUserAsync(Guid userId, PolicyType policyType, OrganizationUserStatusType minStatus = OrganizationUserStatusType.Accepted)
    {
        var result = await QueryOrganizationUserPolicyDetailsAsync(userId, policyType, minStatus);
        return result.ToList();
    }

    public async Task<bool> AnyPoliciesApplicableToUserAsync(Guid userId, PolicyType policyType, OrganizationUserStatusType minStatus = OrganizationUserStatusType.Accepted)
    {
        var result = await QueryOrganizationUserPolicyDetailsAsync(userId, policyType, minStatus);
        return result.Any();
    }

    private async Task<IEnumerable<OrganizationUserPolicyDetails>> QueryOrganizationUserPolicyDetailsAsync(Guid userId, PolicyType policyType, OrganizationUserStatusType minStatus = OrganizationUserStatusType.Accepted)
    {
        var organizationUserPolicyDetails = await _organizationUserRepository.GetByUserIdWithPolicyDetailsAsync(userId, policyType);
        var excludedUserTypes = GetUserTypesExcludedFromPolicy(policyType);
        var orgAbilities = await _applicationCacheService.GetOrganizationAbilitiesAsync();
        return organizationUserPolicyDetails.Where(o =>
            (!orgAbilities.ContainsKey(o.OrganizationId) || orgAbilities[o.OrganizationId].UsePolicies) &&
            o.PolicyEnabled &&
            !excludedUserTypes.Contains(o.OrganizationUserType) &&
            o.OrganizationUserStatus >= minStatus &&
            !o.IsProvider);
    }

    private OrganizationUserType[] GetUserTypesExcludedFromPolicy(PolicyType policyType)
    {
        switch (policyType)
        {
            case PolicyType.MasterPassword:
                return Array.Empty<OrganizationUserType>();
            case PolicyType.RequireSso:
                // If 'EnforceSsoPolicyForAllUsers' is set to true then SSO policy applies to all user types otherwise it does not apply to Owner or Admin
                if (_globalSettings.Sso.EnforceSsoPolicyForAllUsers)
                {
                    return Array.Empty<OrganizationUserType>();
                }
                break;
        }

        return new[] { OrganizationUserType.Owner, OrganizationUserType.Admin };
    }

    private async Task DependsOnSingleOrgAsync(Organization org)
    {
        var singleOrg = await _policyRepository.GetByOrganizationIdTypeAsync(org.Id, PolicyType.SingleOrg);
        if (singleOrg?.Enabled != true)
        {
            throw new BadRequestException("Single Organization policy not enabled.");
        }
    }

    private async Task RequiredBySsoAsync(Organization org)
    {
        var requireSso = await _policyRepository.GetByOrganizationIdTypeAsync(org.Id, PolicyType.RequireSso);
        if (requireSso?.Enabled == true)
        {
            throw new BadRequestException("Single Sign-On Authentication policy is enabled.");
        }
    }

    private async Task RequiredByKeyConnectorAsync(Organization org)
    {
        var ssoConfig = await _ssoConfigRepository.GetByOrganizationIdAsync(org.Id);
        if (ssoConfig?.GetData()?.MemberDecryptionType == MemberDecryptionType.KeyConnector)
        {
            throw new BadRequestException("Key Connector is enabled.");
        }
    }

    private async Task RequiredByAccountRecoveryAsync(Organization org)
    {
        var requireSso = await _policyRepository.GetByOrganizationIdTypeAsync(org.Id, PolicyType.ResetPassword);
        if (requireSso?.Enabled == true)
        {
            throw new BadRequestException("Account recovery policy is enabled.");
        }
    }

    private async Task RequiredByVaultTimeoutAsync(Organization org)
    {
        var vaultTimeout = await _policyRepository.GetByOrganizationIdTypeAsync(org.Id, PolicyType.MaximumVaultTimeout);
        if (vaultTimeout?.Enabled == true)
        {
            throw new BadRequestException("Maximum Vault Timeout policy is enabled.");
        }
    }

    private async Task RequiredBySsoTrustedDeviceEncryptionAsync(Organization org)
    {
        var ssoConfig = await _ssoConfigRepository.GetByOrganizationIdAsync(org.Id);
        if (ssoConfig?.GetData()?.MemberDecryptionType == MemberDecryptionType.TrustedDeviceEncryption)
        {
            throw new BadRequestException("Trusted device encryption is on and requires this policy.");
        }
    }

    private async Task HandleDependentPoliciesAsync(Policy policy, Organization org)
    {
        switch (policy.Type)
        {
            case PolicyType.SingleOrg:
                if (!policy.Enabled)
                {
                    await HasVerifiedDomainsAsync(org);
                    await RequiredBySsoAsync(org);
                    await RequiredByVaultTimeoutAsync(org);
                    await RequiredByKeyConnectorAsync(org);
                    await RequiredByAccountRecoveryAsync(org);
                }
                break;

            case PolicyType.RequireSso:
                if (policy.Enabled)
                {
                    await DependsOnSingleOrgAsync(org);
                }
                else
                {
                    await RequiredByKeyConnectorAsync(org);
                    await RequiredBySsoTrustedDeviceEncryptionAsync(org);
                }
                break;

            case PolicyType.ResetPassword:
                if (!policy.Enabled || policy.GetDataModel<ResetPasswordDataModel>()?.AutoEnrollEnabled == false)
                {
                    await RequiredBySsoTrustedDeviceEncryptionAsync(org);
                }

                if (policy.Enabled)
                {
                    await DependsOnSingleOrgAsync(org);
                }
                break;

            case PolicyType.MaximumVaultTimeout:
                if (policy.Enabled)
                {
                    await DependsOnSingleOrgAsync(org);
                }
                break;
        }
    }

    private async Task HasVerifiedDomainsAsync(Organization org)
    {
        if (_featureService.IsEnabled(FeatureFlagKeys.AccountDeprovisioning)
            && await _organizationHasVerifiedDomainsQuery.HasVerifiedDomainsAsync(org.Id))
        {
            throw new BadRequestException("The Single organization policy is required for organizations that have enabled domain verification.");
        }
    }

    private async Task SetPolicyConfiguration(Policy policy)
    {
        await _policyRepository.UpsertAsync(policy);
        await _eventService.LogPolicyEventAsync(policy, EventType.Policy_Updated);
    }

    private async Task EnablePolicyAsync(Policy policy, Organization org, Guid? savingUserId)
    {
        var currentPolicy = await _policyRepository.GetByIdAsync(policy.Id);
        if (!currentPolicy?.Enabled ?? true)
        {
            var orgUsers = await _organizationUserRepository.GetManyDetailsByOrganizationAsync(policy.OrganizationId);
            var organizationUsersTwoFactorEnabled = await _twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(orgUsers);
            var removableOrgUsers = orgUsers.Where(ou =>
                ou.Status != OrganizationUserStatusType.Invited && ou.Status != OrganizationUserStatusType.Revoked &&
                ou.Type != OrganizationUserType.Owner && ou.Type != OrganizationUserType.Admin &&
                ou.UserId != savingUserId);
            switch (policy.Type)
            {
                case PolicyType.TwoFactorAuthentication:
                    // Reorder by HasMasterPassword to prioritize checking users without a master if they have 2FA enabled
                    foreach (var orgUser in removableOrgUsers.OrderBy(ou => ou.HasMasterPassword))
                    {
                        var userTwoFactorEnabled = organizationUsersTwoFactorEnabled.FirstOrDefault(u => u.user.Id == orgUser.Id).twoFactorIsEnabled;
                        if (!userTwoFactorEnabled)
                        {
                            if (!orgUser.HasMasterPassword)
                            {
                                throw new BadRequestException(
                                    "Policy could not be enabled. Non-compliant members will lose access to their accounts. Identify members without two-step login from the policies column in the members page.");
                            }

                            await _removeOrganizationUserCommand.RemoveUserAsync(policy.OrganizationId, orgUser.Id,
                                savingUserId);
                            await _mailService.SendOrganizationUserRemovedForPolicyTwoStepEmailAsync(
                                org.DisplayName(), orgUser.Email);
                        }
                    }
                    break;
                case PolicyType.SingleOrg:
                    var userOrgs = await _organizationUserRepository.GetManyByManyUsersAsync(
                            removableOrgUsers.Select(ou => ou.UserId.Value));
                    foreach (var orgUser in removableOrgUsers)
                    {
                        if (userOrgs.Any(ou => ou.UserId == orgUser.UserId
                                    && ou.OrganizationId != org.Id
                                    && ou.Status != OrganizationUserStatusType.Invited))
                        {
                            await _removeOrganizationUserCommand.RemoveUserAsync(policy.OrganizationId, orgUser.Id,
                                savingUserId);
                            await _mailService.SendOrganizationUserRemovedForPolicySingleOrgEmailAsync(
                                org.DisplayName(), orgUser.Email);
                        }
                    }
                    break;
                default:
                    break;
            }
        }

        await SetPolicyConfiguration(policy);
    }
}
