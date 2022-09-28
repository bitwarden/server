using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;

namespace Bit.Core.Services;

public class PolicyService : IPolicyService
{
    private readonly IEventService _eventService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IPolicyRepository _policyRepository;
    private readonly ISsoConfigRepository _ssoConfigRepository;
    private readonly IMailService _mailService;

    public PolicyService(
        IEventService eventService,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IPolicyRepository policyRepository,
        ISsoConfigRepository ssoConfigRepository,
        IMailService mailService)
    {
        _eventService = eventService;
        _organizationRepository = organizationRepository;
        _organizationUserRepository = organizationUserRepository;
        _policyRepository = policyRepository;
        _ssoConfigRepository = ssoConfigRepository;
        _mailService = mailService;
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
                }
                break;

            case PolicyType.MaximumVaultTimeout:
                if (policy.Enabled)
                {
                    await DependsOnSingleOrgAsync(org);
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
                    ou.Status != Enums.OrganizationUserStatusType.Invited &&
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
        if (ssoConfig?.GetData()?.KeyConnectorEnabled == true)
        {
            throw new BadRequestException("Key Connector is enabled.");
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
}
