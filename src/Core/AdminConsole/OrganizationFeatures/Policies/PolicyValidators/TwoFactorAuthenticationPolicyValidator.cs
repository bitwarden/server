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
        IRevokeNonCompliantOrganizationUserCommand revokeNonCompliantOrganizationUserCommand)
    {
        _organizationUserRepository = organizationUserRepository;
        _mailService = mailService;
        _organizationRepository = organizationRepository;
        _currentContext = currentContext;
        _twoFactorIsEnabledQuery = twoFactorIsEnabledQuery;
        _revokeNonCompliantOrganizationUserCommand = revokeNonCompliantOrganizationUserCommand;
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

    public Task OnSaveSideEffectsAsync(SavePolicyModel policyUpdate, Policy? currentPolicy) => throw new NotImplementedException();

    private async Task RevokeNonCompliantUsersAsync(Guid organizationId, IActingUser performedBy)
    {
        var organization = await _organizationRepository.GetByIdAsync(organizationId);

        if (organization is null)
        {
            return;
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

        var revocableUsersWithTwoFactorStatus =
            await _twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(currentActiveRevocableOrganizationUsers);

        var nonCompliantUsers = revocableUsersWithTwoFactorStatus
            .Where(x => !x.twoFactorIsEnabled)
            .ToArray();

        if (nonCompliantUsers.Length == 0)
        {
            return;
        }

        if (MembersWithNoMasterPasswordWillLoseAccess(currentActiveRevocableOrganizationUsers, nonCompliantUsers))
        {
            throw new BadRequestException(NonCompliantMembersWillLoseAccessMessage);
        }

        var commandResult = await _revokeNonCompliantOrganizationUserCommand.RevokeNonCompliantOrganizationUsersAsync(
            new RevokeOrganizationUsersRequest(organizationId, nonCompliantUsers.Select(x => x.user), performedBy));

        if (commandResult.HasErrors)
        {
            throw new BadRequestException(string.Join(", ", commandResult.ErrorMessages));
        }

        await Task.WhenAll(nonCompliantUsers.Select(nonCompliantUser =>
            _mailService.SendOrganizationUserRevokedForTwoFactorPolicyEmailAsync(organization.DisplayName(), nonCompliantUser.user.Email)));
    }

    private static bool MembersWithNoMasterPasswordWillLoseAccess(
        IEnumerable<OrganizationUserUserDetails> orgUserDetails,
        IEnumerable<(OrganizationUserUserDetails user, bool isTwoFactorEnabled)> organizationUsersTwoFactorEnabled) =>
            orgUserDetails.Any(x =>
                !x.HasMasterPassword && !organizationUsersTwoFactorEnabled.FirstOrDefault(u => u.user.Id == x.Id)
                    .isTwoFactorEnabled);

    public Task<string> ValidateAsync(PolicyUpdate policyUpdate, Policy? currentPolicy) => Task.FromResult("");
}
