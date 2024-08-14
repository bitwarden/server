using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Repositories;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;

public class SingleOrgPolicyStrategy : IPolicyStrategy
{
    public PolicyType Type => PolicyType.SingleOrg;

    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IMailService _mailService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IPolicyRepository _policyRepository;
    private readonly ISsoConfigRepository _ssoConfigRepository;

    public SingleOrgPolicyStrategy(
        IOrganizationUserRepository organizationUserRepository,
        IMailService mailService,
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository,
        ISsoConfigRepository ssoConfigRepository)
    {
        _organizationUserRepository = organizationUserRepository;
        _mailService = mailService;
        _organizationRepository = organizationRepository;
        _policyRepository = policyRepository;
        _ssoConfigRepository = ssoConfigRepository;
    }

    public async Task HandleEnable(Policy policy, Guid? savingUserId)
    {
        // Remove non-compliant users
        var orgUsers = await _organizationUserRepository.GetManyDetailsByOrganizationAsync(policy.OrganizationId);
        var org = await _organizationRepository.GetByIdAsync(policy.OrganizationId);
        if (org == null)
        {
            throw new NotFoundException("Organization not found.");
        }

        var removableOrgUsers = orgUsers.Where(ou =>
            ou.Status != OrganizationUserStatusType.Invited &&
            ou.Status != OrganizationUserStatusType.Revoked &&
            ou.Type != OrganizationUserType.Owner &&
            ou.Type != OrganizationUserType.Admin &&
            ou.UserId != savingUserId
            ).ToList();

        var userOrgs = await _organizationUserRepository.GetManyByManyUsersAsync(
                removableOrgUsers.Select(ou => ou.UserId!.Value));
        foreach (var orgUser in removableOrgUsers)
        {
            if (userOrgs.Any(ou => ou.UserId == orgUser.UserId
                        && ou.OrganizationId != org.Id
                        && ou.Status != OrganizationUserStatusType.Invited))
            {
                // TODO: OrganizationService causes a circular dependency here, we either pass it into all handlers
                // TODO: like we do with PolicyService at the moment, or we investigate and break that circle
                // await _organizationService.DeleteUserAsync(policy.OrganizationId, orgUser.Id,
                //     savingUserId);
                await _mailService.SendOrganizationUserRemovedForPolicySingleOrgEmailAsync(
                    org.DisplayName(), orgUser.Email);
            }
        }
    }

    public async Task HandleDisable(Policy policy, Guid? savingUserId)
    {
        // Do not allow this policy to be disabled if a dependent policy is still enabled
        var policies = await _policyRepository.GetManyByOrganizationIdAsync(policy.OrganizationId);
        if (policies.Any(p => p.Type == PolicyType.RequireSso && p.Enabled))
        {
            throw new BadRequestException("Single Sign-On Authentication policy is enabled.");
        }

        if (policies.Any(p => p.Type == PolicyType.MaximumVaultTimeout && p.Enabled))
        {
            throw new BadRequestException("Maximum Vault Timeout policy is enabled.");
        }

        if (policies.Any(p => p.Type == PolicyType.ResetPassword && p.Enabled))
        {
            throw new BadRequestException("Account Recovery policy is enabled.");
        }

        // Do not allow this policy to be disabled if Key Connector is being used
        var ssoConfig = await _ssoConfigRepository.GetByOrganizationIdAsync(policy.OrganizationId);
        if (ssoConfig?.GetData()?.MemberDecryptionType == MemberDecryptionType.KeyConnector)
        {
            throw new BadRequestException("Key Connector is enabled.");
        }
    }
}
