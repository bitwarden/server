#nullable enable
using Bit.Api.KeyManagement.Models.Requests;
using Bit.Core;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Commands.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.KeyManagement.Controllers;

[Route("accounts/key-management")]
[Authorize("Application")]
public class AccountsKeyManagementController : Controller
{
    private readonly IEmergencyAccessRepository _emergencyAccessRepository;
    private readonly IFeatureService _featureService;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IRegenerateUserAsymmetricKeysCommand _regenerateUserAsymmetricKeysCommand;
    private readonly IUserService _userService;

    public AccountsKeyManagementController(IUserService userService,
        IFeatureService featureService,
        IOrganizationUserRepository organizationUserRepository,
        IEmergencyAccessRepository emergencyAccessRepository,
        IRegenerateUserAsymmetricKeysCommand regenerateUserAsymmetricKeysCommand)
    {
        _userService = userService;
        _featureService = featureService;
        _regenerateUserAsymmetricKeysCommand = regenerateUserAsymmetricKeysCommand;
        _organizationUserRepository = organizationUserRepository;
        _emergencyAccessRepository = emergencyAccessRepository;
    }

    [HttpPost("regenerate-keys")]
    public async Task RegenerateKeysAsync([FromBody] KeyRegenerationRequestModel request)
    {
        if (!_featureService.IsEnabled(FeatureFlagKeys.PrivateKeyRegeneration))
        {
            throw new NotFoundException();
        }

        var user = await _userService.GetUserByPrincipalAsync(User) ?? throw new UnauthorizedAccessException();
        var usersOrganizationAccounts = await _organizationUserRepository.GetManyByUserAsync(user.Id);
        var designatedEmergencyAccess = await _emergencyAccessRepository.GetManyDetailsByGranteeIdAsync(user.Id);
        await _regenerateUserAsymmetricKeysCommand.RegenerateKeysAsync(request.ToUserAsymmetricKeys(user.Id),
            usersOrganizationAccounts, designatedEmergencyAccess);
    }
}
