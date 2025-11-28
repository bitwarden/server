using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Requests;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Repositories;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

public class SingleOrgPolicyValidator : IPolicyValidator, IPolicyValidationEvent, IOnPolicyPreUpdateEvent
{
    public PolicyType Type => PolicyType.SingleOrg;
    private const string OrganizationNotFoundErrorMessage = "Organization not found.";
    private const string ClaimedDomainSingleOrganizationRequiredErrorMessage = "The Single organization policy is required for organizations that have enabled domain verification.";

    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IMailService _mailService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ISsoConfigRepository _ssoConfigRepository;
    private readonly ICurrentContext _currentContext;
    private readonly IOrganizationHasVerifiedDomainsQuery _organizationHasVerifiedDomainsQuery;
    private readonly IRevokeNonCompliantOrganizationUserCommand _revokeNonCompliantOrganizationUserCommand;

    public SingleOrgPolicyValidator(
        IOrganizationUserRepository organizationUserRepository,
        IMailService mailService,
        IOrganizationRepository organizationRepository,
        ISsoConfigRepository ssoConfigRepository,
        ICurrentContext currentContext,
        IOrganizationHasVerifiedDomainsQuery organizationHasVerifiedDomainsQuery,
        IRevokeNonCompliantOrganizationUserCommand revokeNonCompliantOrganizationUserCommand)
    {
        _organizationUserRepository = organizationUserRepository;
        _mailService = mailService;
        _organizationRepository = organizationRepository;
        _ssoConfigRepository = ssoConfigRepository;
        _currentContext = currentContext;
        _organizationHasVerifiedDomainsQuery = organizationHasVerifiedDomainsQuery;
        _revokeNonCompliantOrganizationUserCommand = revokeNonCompliantOrganizationUserCommand;
    }

    public IEnumerable<PolicyType> RequiredPolicies => [];

    public async Task<string> ValidateAsync(SavePolicyModel policyRequest, Policy? currentPolicy)
    {
        return await ValidateAsync(policyRequest.PolicyUpdate, currentPolicy);
    }

    public async Task ExecutePreUpsertSideEffectAsync(SavePolicyModel policyRequest, Policy? currentPolicy)
    {
        await OnSaveSideEffectsAsync(policyRequest.PolicyUpdate, currentPolicy);
    }

    public async Task OnSaveSideEffectsAsync(PolicyUpdate policyUpdate, Policy? currentPolicy)
    {
        if (currentPolicy is not { Enabled: true } && policyUpdate is { Enabled: true })
        {
            var currentUser = _currentContext.UserId ?? Guid.Empty;
            var isOwnerOrProvider = await _currentContext.OrganizationOwner(policyUpdate.OrganizationId);
            await RevokeNonCompliantUsersAsync(policyUpdate.OrganizationId, policyUpdate.PerformedBy ?? new StandardUser(currentUser, isOwnerOrProvider));
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

        var allRevocableUserOrgs = await _organizationUserRepository.GetManyByManyUsersAsync(
            currentActiveRevocableOrganizationUsers.Select(ou => ou.UserId!.Value));
        var usersToRevoke = currentActiveRevocableOrganizationUsers.Where(ou =>
            allRevocableUserOrgs.Any(uo => uo.UserId == ou.UserId &&
                uo.OrganizationId != organizationId &&
                uo.Status != OrganizationUserStatusType.Invited)).ToList();

        var commandResult = await _revokeNonCompliantOrganizationUserCommand.RevokeNonCompliantOrganizationUsersAsync(
            new RevokeOrganizationUsersRequest(organizationId, usersToRevoke, performedBy));

        if (commandResult.HasErrors)
        {
            throw new BadRequestException(string.Join(", ", commandResult.ErrorMessages));
        }

        await Task.WhenAll(usersToRevoke.Select(x =>
            _mailService.SendOrganizationUserRevokedForPolicySingleOrgEmailAsync(organization.DisplayName(), x.Email)));
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

            if (await _organizationHasVerifiedDomainsQuery.HasVerifiedDomainsAsync(policyUpdate.OrganizationId))
            {
                return ClaimedDomainSingleOrganizationRequiredErrorMessage;
            }
        }

        return string.Empty;
    }
}
