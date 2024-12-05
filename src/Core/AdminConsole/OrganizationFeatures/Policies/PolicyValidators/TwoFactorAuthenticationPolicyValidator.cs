#nullable enable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Requests;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

public class TwoFactorAuthenticationPolicyValidator : IPolicyValidator
{
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IMailService _mailService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ICurrentContext _currentContext;
    private readonly ITwoFactorIsEnabledQuery _twoFactorIsEnabledQuery;
    private readonly IRemoveOrganizationUserCommand _removeOrganizationUserCommand;
    private readonly IFeatureService _featureService;
    private readonly IRevokeNonCompliantOrganizationUserCommand _revokeNonCompliantOrganizationUserCommand;

    public const string NonCompliantMembersWillLoseAccessMessage = "Policy could not be enabled. Non-compliant members will lose access to their accounts. Identify members without two-step login from the policies column in the members page.";

    public PolicyType Type => PolicyType.TwoFactorAuthentication;
    public IEnumerable<PolicyType> RequiredPolicies => [];

    public TwoFactorAuthenticationPolicyValidator(
        IOrganizationUserRepository organizationUserRepository,
        IMailService mailService,
        IOrganizationRepository organizationRepository,
        ICurrentContext currentContext,
        ITwoFactorIsEnabledQuery twoFactorIsEnabledQuery,
        IRemoveOrganizationUserCommand removeOrganizationUserCommand,
        IFeatureService featureService,
        IRevokeNonCompliantOrganizationUserCommand revokeNonCompliantOrganizationUserCommand)
    {
        _organizationUserRepository = organizationUserRepository;
        _mailService = mailService;
        _organizationRepository = organizationRepository;
        _currentContext = currentContext;
        _twoFactorIsEnabledQuery = twoFactorIsEnabledQuery;
        _removeOrganizationUserCommand = removeOrganizationUserCommand;
        _featureService = featureService;
        _revokeNonCompliantOrganizationUserCommand = revokeNonCompliantOrganizationUserCommand;
    }

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

        var organizationUsersTwoFactorEnabled =
            await _twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(currentActiveRevocableOrganizationUsers);

        if (NonCompliantMembersWillLoseAccess(currentActiveRevocableOrganizationUsers, organizationUsersTwoFactorEnabled))
        {
            throw new BadRequestException(NonCompliantMembersWillLoseAccessMessage);
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
        var org = await _organizationRepository.GetByIdAsync(organizationId);
        var savingUserId = _currentContext.UserId;

        var orgUsers = await _organizationUserRepository.GetManyDetailsByOrganizationAsync(organizationId);
        var organizationUsersTwoFactorEnabled = await _twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(orgUsers);
        var removableOrgUsers = orgUsers.Where(ou =>
            ou.Status != OrganizationUserStatusType.Invited && ou.Status != OrganizationUserStatusType.Revoked &&
            ou.Type != OrganizationUserType.Owner && ou.Type != OrganizationUserType.Admin &&
            ou.UserId != savingUserId);

        // Reorder by HasMasterPassword to prioritize checking users without a master if they have 2FA enabled
        foreach (var orgUser in removableOrgUsers.OrderBy(ou => ou.HasMasterPassword))
        {
            var userTwoFactorEnabled = organizationUsersTwoFactorEnabled.FirstOrDefault(u => u.user.Id == orgUser.Id)
                .twoFactorIsEnabled;
            if (!userTwoFactorEnabled)
            {
                if (!orgUser.HasMasterPassword)
                {
                    throw new BadRequestException(
                        "Policy could not be enabled. Non-compliant members will lose access to their accounts. Identify members without two-step login from the policies column in the members page.");
                }

                await _removeOrganizationUserCommand.RemoveUserAsync(organizationId, orgUser.Id,
                    savingUserId);

                await _mailService.SendOrganizationUserRemovedForPolicyTwoStepEmailAsync(
                    org!.DisplayName(), orgUser.Email);
            }
        }
    }

    private static bool NonCompliantMembersWillLoseAccess(
        IEnumerable<OrganizationUserUserDetails> orgUserDetails,
        IEnumerable<(OrganizationUserUserDetails user, bool isTwoFactorEnabled)> organizationUsersTwoFactorEnabled) =>
            orgUserDetails.Any(x =>
                !x.HasMasterPassword && !organizationUsersTwoFactorEnabled.FirstOrDefault(u => u.user.Id == x.Id)
                    .isTwoFactorEnabled);

    public Task<string> ValidateAsync(PolicyUpdate policyUpdate, Policy? currentPolicy) => Task.FromResult("");
}
