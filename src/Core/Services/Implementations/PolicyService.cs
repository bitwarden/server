using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Models.Data.Organizations.Policies;
using Bit.Core.Repositories;
using Bit.Core.Settings;

namespace Bit.Core.Services;

public class PolicyService : IPolicyService
{
    private readonly IEventService _eventService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IPolicyRepository _policyRepository;
    private readonly ISsoConfigRepository _ssoConfigRepository;
    private readonly IMailService _mailService;
    private readonly GlobalSettings _globalSettings;

    private IEnumerable<OrganizationUserPolicyDetails> _cachedOrganizationUserPolicyDetails;

    public PolicyService(
        IEventService eventService,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IPolicyRepository policyRepository,
        ISsoConfigRepository ssoConfigRepository,
        IMailService mailService,
        GlobalSettings globalSettings)
    {
        _eventService = eventService;
        _organizationRepository = organizationRepository;
        _organizationUserRepository = organizationUserRepository;
        _policyRepository = policyRepository;
        _ssoConfigRepository = ssoConfigRepository;
        _mailService = mailService;
        _globalSettings = globalSettings;
    }

    public async Task SaveAsync(Policy policy, IUserService userService, IOrganizationService organizationService,
        Guid? savingUserId)
    {
        var org = await _organizationRepository.GetByIdAsync(policy.OrganizationId);
        if (org == null)
        {
            throw new BadRequestException("Organization not found");
        }

        if (!org.UsePolicies)
        {
            throw new BadRequestException("This organization cannot use policies.");
        }

        // Handle dependent policy checks
        switch (policy.Type)
        {
            case PolicyType.SingleOrg:
                if (!policy.Enabled)
                {
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

            // Activate Autofill is only available to Enterprise 2020-current plans
            case PolicyType.ActivateAutofill:
                if (policy.Enabled)
                {
                    LockedTo2020Plan(org);
                }
                break;
        }

        var now = DateTime.UtcNow;
        if (policy.Id == default(Guid))
        {
            policy.CreationDate = now;
        }

        if (policy.Enabled)
        {
            var currentPolicy = await _policyRepository.GetByIdAsync(policy.Id);
            if (!currentPolicy?.Enabled ?? true)
            {
                var orgUsers = await _organizationUserRepository.GetManyDetailsByOrganizationAsync(
                    policy.OrganizationId);
                var removableOrgUsers = orgUsers.Where(ou =>
                    ou.Status != Enums.OrganizationUserStatusType.Invited && ou.Status != Enums.OrganizationUserStatusType.Revoked &&
                    ou.Type != Enums.OrganizationUserType.Owner && ou.Type != Enums.OrganizationUserType.Admin &&
                    ou.UserId != savingUserId);
                switch (policy.Type)
                {
                    case Enums.PolicyType.TwoFactorAuthentication:
                        foreach (var orgUser in removableOrgUsers)
                        {
                            if (!await userService.TwoFactorIsEnabledAsync(orgUser))
                            {
                                await organizationService.DeleteUserAsync(policy.OrganizationId, orgUser.Id,
                                    savingUserId);
                                await _mailService.SendOrganizationUserRemovedForPolicyTwoStepEmailAsync(
                                    org.Name, orgUser.Email);
                            }
                        }
                        break;
                    case Enums.PolicyType.SingleOrg:
                        var userOrgs = await _organizationUserRepository.GetManyByManyUsersAsync(
                                removableOrgUsers.Select(ou => ou.UserId.Value));
                        foreach (var orgUser in removableOrgUsers)
                        {
                            if (userOrgs.Any(ou => ou.UserId == orgUser.UserId
                                        && ou.OrganizationId != org.Id
                                        && ou.Status != OrganizationUserStatusType.Invited))
                            {
                                await organizationService.DeleteUserAsync(policy.OrganizationId, orgUser.Id,
                                    savingUserId);
                                await _mailService.SendOrganizationUserRemovedForPolicySingleOrgEmailAsync(
                                    org.Name, orgUser.Email);
                            }
                        }
                        break;
                    default:
                        break;
                }
            }
        }
        policy.RevisionDate = now;
        await _policyRepository.UpsertAsync(policy);
        await _eventService.LogPolicyEventAsync(policy, Enums.EventType.Policy_Updated);
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

    private async Task<IEnumerable<OrganizationUserPolicyDetails>> QueryOrganizationUserPolicyDetailsAsync(Guid userId, PolicyType? policyType, OrganizationUserStatusType minStatus = OrganizationUserStatusType.Accepted)
    {
        // Check if the cached policies are available
        if (_cachedOrganizationUserPolicyDetails == null)
        {
            // Cached policies not available, retrieve from the repository
            _cachedOrganizationUserPolicyDetails = await _organizationUserRepository.GetByUserIdWithPolicyDetailsAsync(userId);
        }

        var excludedUserTypes = GetUserTypesExcludedFromPolicy(policyType);
        return _cachedOrganizationUserPolicyDetails.Where(o =>
            (policyType == null || o.PolicyType == policyType) &&
            o.PolicyEnabled &&
            !excludedUserTypes.Contains(o.OrganizationUserType) &&
            o.OrganizationUserStatus >= minStatus &&
            !o.IsProvider);
    }

    private OrganizationUserType[] GetUserTypesExcludedFromPolicy(PolicyType? policyType)
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

    private void LockedTo2020Plan(Organization org)
    {
        if (org.PlanType != PlanType.EnterpriseAnnually && org.PlanType != PlanType.EnterpriseMonthly)
        {
            throw new BadRequestException("This policy is only available to 2020 Enterprise plans.");
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
}
