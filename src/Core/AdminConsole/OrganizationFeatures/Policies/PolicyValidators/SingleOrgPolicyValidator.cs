#nullable enable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Repositories;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

public class SingleOrgPolicyValidator : IPolicyValidator
{
    public PolicyType Type => PolicyType.SingleOrg;

    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IMailService _mailService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ISsoConfigRepository _ssoConfigRepository;
    private readonly ICurrentContext _currentContext;
    private readonly IRemoveOrganizationUserCommand _removeOrganizationUserCommand;

    public SingleOrgPolicyValidator(
        IOrganizationUserRepository organizationUserRepository,
        IMailService mailService,
        IOrganizationRepository organizationRepository,
        ISsoConfigRepository ssoConfigRepository,
        ICurrentContext currentContext,
        IRemoveOrganizationUserCommand removeOrganizationUserCommand)
    {
        _organizationUserRepository = organizationUserRepository;
        _mailService = mailService;
        _organizationRepository = organizationRepository;
        _ssoConfigRepository = ssoConfigRepository;
        _currentContext = currentContext;
        _removeOrganizationUserCommand = removeOrganizationUserCommand;
    }

    public IEnumerable<PolicyType> RequiredPolicies => [];

    public async Task OnSaveSideEffectsAsync(PolicyUpdate policyUpdate, Policy? currentPolicy)
    {
        if (currentPolicy is not { Enabled: true } && policyUpdate is { Enabled: true })
        {
            await RemoveNonCompliantUsersAsync(policyUpdate.OrganizationId);
        }
    }

    private async Task RemoveNonCompliantUsersAsync(Guid organizationId)
    {
        // Remove non-compliant users
        var savingUserId = _currentContext.UserId;
        var orgUsers = await _organizationUserRepository.GetManyByOrganizationAsync(organizationId, null);
        var org = await _organizationRepository.GetByIdAsync(organizationId);
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
                await _removeOrganizationUserCommand.RemoveUserAsync(organizationId, orgUser.Id,
                    savingUserId);

                await _mailService.SendOrganizationUserRemovedForPolicySingleOrgEmailAsync(
                    org.DisplayName(), orgUser.Email);
            }
        }
    }

    public async Task<string> ValidateAsync(PolicyUpdate policyUpdate, Policy? currentPolicy)
    {
        if (policyUpdate is not { Enabled: true })
        {
            var ssoConfig = await _ssoConfigRepository.GetByOrganizationIdAsync(policyUpdate.OrganizationId);
            return ssoConfig.ValidateDecryptionOptionsNotEnabled([MemberDecryptionType.KeyConnector]);
        }

        return "";
    }
}
