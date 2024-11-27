#nullable enable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Requests;
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
    private const string OrganizationNotFoundErrorMessage = "Organization not found.";
    private const string ClaimedDomainSingleOrganizationRequiredErrorMessage = "The Single organization policy is required for organizations that have enabled domain verification.";

    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IMailService _mailService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ISsoConfigRepository _ssoConfigRepository;
    private readonly ICurrentContext _currentContext;
    private readonly IFeatureService _featureService;
    private readonly IRemoveOrganizationUserCommand _removeOrganizationUserCommand;
    private readonly IOrganizationHasVerifiedDomainsQuery _organizationHasVerifiedDomainsQuery;
    private readonly IRevokeNonCompliantOrganizationUserCommand _revokeNonCompliantOrganizationUserCommand;

    public SingleOrgPolicyValidator(
        IOrganizationUserRepository organizationUserRepository,
        IMailService mailService,
        IOrganizationRepository organizationRepository,
        ISsoConfigRepository ssoConfigRepository,
        ICurrentContext currentContext,
        IFeatureService featureService,
        IRemoveOrganizationUserCommand removeOrganizationUserCommand,
        IOrganizationHasVerifiedDomainsQuery organizationHasVerifiedDomainsQuery,
        IRevokeNonCompliantOrganizationUserCommand revokeNonCompliantOrganizationUserCommand)
    {
        _organizationUserRepository = organizationUserRepository;
        _mailService = mailService;
        _organizationRepository = organizationRepository;
        _ssoConfigRepository = ssoConfigRepository;
        _currentContext = currentContext;
        _featureService = featureService;
        _removeOrganizationUserCommand = removeOrganizationUserCommand;
        _organizationHasVerifiedDomainsQuery = organizationHasVerifiedDomainsQuery;
        _revokeNonCompliantOrganizationUserCommand = revokeNonCompliantOrganizationUserCommand;
    }

    public IEnumerable<PolicyType> RequiredPolicies => [];

    public async Task OnSaveSideEffectsAsync(PolicyUpdate policyUpdate, Policy? currentPolicy)
    {
        if (currentPolicy is not { Enabled: true } && policyUpdate is { Enabled: true })
        {
            if (_featureService.IsEnabled(FeatureFlagKeys.AccountDeprovisioning))
            {
                var currentUser = _currentContext.UserId ?? Guid.Empty;
                var isOwnerOrProvider = await _currentContext.OrganizationOwner(policyUpdate.OrganizationId);
                await RevokeNonCompliantUsersAsync(policyUpdate.OrganizationId, policyUpdate.PerformedBy ?? new StandardUser(currentUser, isOwnerOrProvider));
            }
            else
            {
                await RemoveNonCompliantUsersAsync(policyUpdate.OrganizationId);
            }
        }
    }

    private async Task RevokeNonCompliantUsersAsync(Guid organizationId, IActingUser performedBy)
    {
        var organization = await _organizationRepository.GetByIdAsync(organizationId);

        if (organization is null)
        {
            throw new NotFoundException(OrganizationNotFoundErrorMessage);
        }

        var currentActiveRevocableOrganizationUsers =
            (await _organizationUserRepository.GetManyDetailsByOrganizationAsync(organizationId))
            .Where(ou => ou.Status != OrganizationUserStatusType.Invited &&
                         ou.Status != OrganizationUserStatusType.Revoked &&
                         ou.Type != OrganizationUserType.Owner &&
                         ou.Type != OrganizationUserType.Admin &&
                         !(performedBy is StandardUser stdUser && stdUser.UserId == ou.UserId))
            .ToList();

        if (currentActiveRevocableOrganizationUsers.Count == 0)
        {
            return;
        }

        var commandResult = await _revokeNonCompliantOrganizationUserCommand.RevokeNonCompliantOrganizationUsersAsync(
            new RevokeOrganizationUsersRequest(organizationId, currentActiveRevocableOrganizationUsers, performedBy));

        if (commandResult.HasErrors)
        {
            throw new BadRequestException(string.Join(", ", commandResult.ErrorMessages));
        }

        await Task.WhenAll(currentActiveRevocableOrganizationUsers.Select(x =>
            _mailService.SendOrganizationUserRevokedForPolicySingleOrgEmailAsync(organization.DisplayName(), x.Email)));
    }

    private async Task RemoveNonCompliantUsersAsync(Guid organizationId)
    {
        // Remove non-compliant users
        var savingUserId = _currentContext.UserId;
        // Note: must get OrganizationUserUserDetails so that Email is always populated from the User object
        var orgUsers = await _organizationUserRepository.GetManyDetailsByOrganizationAsync(organizationId);
        var org = await _organizationRepository.GetByIdAsync(organizationId);
        if (org == null)
        {
            throw new NotFoundException(OrganizationNotFoundErrorMessage);
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
                await _removeOrganizationUserCommand.RemoveUserAsync(organizationId, orgUser.Id, savingUserId);

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

            var validateDecryptionErrorMessage = ssoConfig.ValidateDecryptionOptionsNotEnabled([MemberDecryptionType.KeyConnector]);

            if (!string.IsNullOrWhiteSpace(validateDecryptionErrorMessage))
            {
                return validateDecryptionErrorMessage;
            }

            if (_featureService.IsEnabled(FeatureFlagKeys.AccountDeprovisioning)
                && await _organizationHasVerifiedDomainsQuery.HasVerifiedDomainsAsync(policyUpdate.OrganizationId))
            {
                return ClaimedDomainSingleOrganizationRequiredErrorMessage;
            }
        }

        return string.Empty;
    }
}
