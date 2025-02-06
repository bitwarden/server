using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Api.Auth.Models.Request;
using Bit.Api.Auth.Models.Response;
using Bit.Api.Models.Response;
using Bit.Api.Vault.Models.Response;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Services;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Auth.Controllers;

[Route("emergency-access")]
[Authorize("Application")]
public class EmergencyAccessController : Controller
{
    private readonly IUserService _userService;
    private readonly IEmergencyAccessRepository _emergencyAccessRepository;
    private readonly IEmergencyAccessService _emergencyAccessService;
    private readonly IGlobalSettings _globalSettings;

    public EmergencyAccessController(
        IUserService userService,
        IEmergencyAccessRepository emergencyAccessRepository,
        IEmergencyAccessService emergencyAccessService,
        IGlobalSettings globalSettings)
    {
        _userService = userService;
        _emergencyAccessRepository = emergencyAccessRepository;
        _emergencyAccessService = emergencyAccessService;
        _globalSettings = globalSettings;
    }

    [HttpGet("trusted")]
    public async Task<ListResponseModel<EmergencyAccessGranteeDetailsResponseModel>> GetContacts()
    {
        var userId = _userService.GetProperUserId(User);
        var granteeDetails = await _emergencyAccessRepository.GetManyDetailsByGrantorIdAsync(userId.Value);

        var responses = granteeDetails.Select(d =>
            new EmergencyAccessGranteeDetailsResponseModel(d));

        return new ListResponseModel<EmergencyAccessGranteeDetailsResponseModel>(responses);
    }

    [HttpGet("granted")]
    public async Task<ListResponseModel<EmergencyAccessGrantorDetailsResponseModel>> GetGrantees()
    {
        var userId = _userService.GetProperUserId(User);
        var granteeDetails = await _emergencyAccessRepository.GetManyDetailsByGranteeIdAsync(userId.Value);

        var responses = granteeDetails.Select(d => new EmergencyAccessGrantorDetailsResponseModel(d));

        return new ListResponseModel<EmergencyAccessGrantorDetailsResponseModel>(responses);
    }

    [HttpGet("{id}")]
    public async Task<EmergencyAccessGranteeDetailsResponseModel> Get(Guid id)
    {
        var userId = _userService.GetProperUserId(User);
        var result = await _emergencyAccessService.GetAsync(id, userId.Value);
        return new EmergencyAccessGranteeDetailsResponseModel(result);
    }

    [HttpGet("{id}/policies")]
    public async Task<ListResponseModel<PolicyResponseModel>> Policies(Guid id)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        var policies = await _emergencyAccessService.GetPoliciesAsync(id, user);
        var responses = policies.Select<Policy, PolicyResponseModel>(policy => new PolicyResponseModel(policy));
        return new ListResponseModel<PolicyResponseModel>(responses);
    }

    [HttpPut("{id}")]
    [HttpPost("{id}")]
    public async Task Put(Guid id, [FromBody] EmergencyAccessUpdateRequestModel model)
    {
        var emergencyAccess = await _emergencyAccessRepository.GetByIdAsync(id);
        if (emergencyAccess == null)
        {
            throw new NotFoundException();
        }

        var user = await _userService.GetUserByPrincipalAsync(User);
        await _emergencyAccessService.SaveAsync(model.ToEmergencyAccess(emergencyAccess), user);
    }

    [HttpDelete("{id}")]
    [HttpPost("{id}/delete")]
    public async Task Delete(Guid id)
    {
        var userId = _userService.GetProperUserId(User);
        await _emergencyAccessService.DeleteAsync(id, userId.Value);
    }

    [HttpPost("invite")]
    public async Task Invite([FromBody] EmergencyAccessInviteRequestModel model)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        await _emergencyAccessService.InviteAsync(user, model.Email, model.Type.Value, model.WaitTimeDays);
    }

    [HttpPost("{id}/reinvite")]
    public async Task Reinvite(Guid id)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        await _emergencyAccessService.ResendInviteAsync(user, id);
    }

    [HttpPost("{id}/accept")]
    public async Task Accept(Guid id, [FromBody] OrganizationUserAcceptRequestModel model)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        await _emergencyAccessService.AcceptUserAsync(id, user, model.Token, _userService);
    }

    [HttpPost("{id}/confirm")]
    public async Task Confirm(Guid id, [FromBody] OrganizationUserConfirmRequestModel model)
    {
        var userId = _userService.GetProperUserId(User);
        await _emergencyAccessService.ConfirmUserAsync(id, model.Key, userId.Value);
    }

    [HttpPost("{id}/initiate")]
    public async Task Initiate(Guid id)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        await _emergencyAccessService.InitiateAsync(id, user);
    }

    [HttpPost("{id}/approve")]
    public async Task Accept(Guid id)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        await _emergencyAccessService.ApproveAsync(id, user);
    }

    [HttpPost("{id}/reject")]
    public async Task Reject(Guid id)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        await _emergencyAccessService.RejectAsync(id, user);
    }

    [HttpPost("{id}/takeover")]
    public async Task<EmergencyAccessTakeoverResponseModel> Takeover(Guid id)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        var (result, grantor) = await _emergencyAccessService.TakeoverAsync(id, user);
        return new EmergencyAccessTakeoverResponseModel(result, grantor);
    }

    [HttpPost("{id}/password")]
    public async Task Password(Guid id, [FromBody] EmergencyAccessPasswordRequestModel model)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        await _emergencyAccessService.PasswordAsync(id, user, model.NewMasterPasswordHash, model.Key);
    }

    [HttpPost("{id}/view")]
    public async Task<EmergencyAccessViewResponseModel> ViewCiphers(Guid id)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        var viewResult = await _emergencyAccessService.ViewAsync(id, user);
        return new EmergencyAccessViewResponseModel(_globalSettings, viewResult.EmergencyAccess, viewResult.Ciphers);
    }

    [HttpGet("{id}/{cipherId}/attachment/{attachmentId}")]
    public async Task<AttachmentResponseModel> GetAttachmentData(Guid id, Guid cipherId, string attachmentId)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        var result =
            await _emergencyAccessService.GetAttachmentDownloadAsync(id, cipherId, attachmentId, user);
        return new AttachmentResponseModel(result);
    }
}
