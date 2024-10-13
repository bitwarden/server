#nullable enable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
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

    public PolicyType Type => PolicyType.TwoFactorAuthentication;

    public TwoFactorAuthenticationPolicyValidator(
        IOrganizationUserRepository organizationUserRepository,
        IMailService mailService,
        IOrganizationRepository organizationRepository,
        ICurrentContext currentContext,
        ITwoFactorIsEnabledQuery twoFactorIsEnabledQuery)
    {
        _organizationUserRepository = organizationUserRepository;
        _mailService = mailService;
        _organizationRepository = organizationRepository;
        _currentContext = currentContext;
        _twoFactorIsEnabledQuery = twoFactorIsEnabledQuery;
    }

    public async Task OnSaveSideEffectsAsync(Policy? currentPolicy, Policy modifiedPolicy, IOrganizationService organizationService)
    {
        if (currentPolicy is not { Enabled: true } && modifiedPolicy is { Enabled: true })
        {
            await RemoveNonCompliantUsersAsync(modifiedPolicy.OrganizationId, organizationService);
        }
    }

    private async Task RemoveNonCompliantUsersAsync(Guid organizationId, IOrganizationService organizationService)
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

                // TODO: enable this once AC-607 is merged to fix this circular dependency
                // await _removeOrganizationUserCommand.RemoveUserAsync(organizationId, orgUser.Id,
                //     savingUserId);

                // In the meantime we use the underlying logic in OrganizationService
                await organizationService.RemoveUserAsync(organizationId, orgUser.Id, savingUserId);

                await _mailService.SendOrganizationUserRemovedForPolicyTwoStepEmailAsync(
                    org!.DisplayName(), orgUser.Email);
            }
        }
    }
}
