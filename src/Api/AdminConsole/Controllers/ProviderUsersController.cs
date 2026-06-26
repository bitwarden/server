// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Api.AdminConsole.Authorization;
using Bit.Api.AdminConsole.Authorization.Providers.Requirements;
using Bit.Api.AdminConsole.Models.Request.Providers;
using Bit.Api.AdminConsole.Models.Response.Providers;
using Bit.Api.Models.Response;
using Bit.Core.AdminConsole.Models.Business.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.AdminConsole.Controllers;

[Route("providers/{providerId:guid}/users")]
[Authorize("Application")]
public class ProviderUsersController : Controller
{
    private readonly IProviderUserRepository _providerUserRepository;
    private readonly IProviderService _providerService;
    private readonly IUserService _userService;

    public ProviderUsersController(
        IProviderUserRepository providerUserRepository,
        IProviderService providerService,
        IUserService userService)
    {
        _providerUserRepository = providerUserRepository;
        _providerService = providerService;
        _userService = userService;
    }

    [HttpGet("{id:guid}")]
    [Authorize<ManageProviderUsersRequirement>]
    public async Task<ProviderUserResponseModel> Get(Guid providerId, Guid id)
    {
        var providerUser = await _providerUserRepository.GetByIdAsync(id);
        if (providerUser == null || providerUser.ProviderId != providerId)
        {
            throw new NotFoundException();
        }

        return new ProviderUserResponseModel(providerUser);
    }

    [HttpGet("")]
    [Authorize<ManageProviderUsersRequirement>]
    public async Task<ListResponseModel<ProviderUserUserDetailsResponseModel>> GetAll(Guid providerId)
    {
        var providerUsers = await _providerUserRepository.GetManyDetailsByProviderAsync(providerId);
        var responses = providerUsers.Select(o => new ProviderUserUserDetailsResponseModel(o));
        return new ListResponseModel<ProviderUserUserDetailsResponseModel>(responses);
    }

    [HttpPost("invite")]
    [Authorize<ManageProviderUsersRequirement>]
    public async Task Invite(Guid providerId, [FromBody] ProviderUserInviteRequestModel model)
    {
        var invite = ProviderUserInviteFactory.CreateInitialInvite(model.Emails, model.Type.Value,
            _userService.GetProperUserId(User).Value, providerId);
        await _providerService.InviteUserAsync(invite);
    }

    [HttpPost("reinvite")]
    [Authorize<ManageProviderUsersRequirement>]
    public async Task<ListResponseModel<ProviderUserBulkResponseModel>> BulkReinvite(Guid providerId, [FromBody] ProviderUserBulkRequestModel model)
    {
        var invite = ProviderUserInviteFactory.CreateReinvite(model.Ids, _userService.GetProperUserId(User).Value, providerId);
        var result = await _providerService.ResendInvitesAsync(invite);
        return new ListResponseModel<ProviderUserBulkResponseModel>(
            result.Select(t => new ProviderUserBulkResponseModel(t.Item1.Id, t.Item2)));
    }

    [HttpPost("{id:guid}/reinvite")]
    [Authorize<ManageProviderUsersRequirement>]
    public async Task Reinvite(Guid providerId, Guid id)
    {
        var invite = ProviderUserInviteFactory.CreateReinvite(new[] { id },
            _userService.GetProperUserId(User).Value, providerId);
        await _providerService.ResendInvitesAsync(invite);
    }

    [HttpPost("{id:guid}/accept")]
    [NoopAuthorize]
    public async Task Accept(Guid providerId, Guid id, [FromBody] ProviderUserAcceptRequestModel model)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        await _providerService.AcceptUserAsync(id, user, model.Token);
    }

    [HttpPost("{id:guid}/confirm")]
    [Authorize<ManageProviderUsersRequirement>]
    public async Task Confirm(Guid providerId, Guid id, [FromBody] ProviderUserConfirmRequestModel model)
    {
        var userId = _userService.GetProperUserId(User);
        await _providerService.ConfirmUsersAsync(providerId, new Dictionary<Guid, string> { [id] = model.Key }, userId.Value);
    }

    [HttpPost("confirm")]
    [Authorize<ManageProviderUsersRequirement>]
    public async Task<ListResponseModel<ProviderUserBulkResponseModel>> BulkConfirm(Guid providerId,
        [FromBody] ProviderUserBulkConfirmRequestModel model)
    {
        var userId = _userService.GetProperUserId(User);
        var results = await _providerService.ConfirmUsersAsync(providerId, model.ToDictionary(), userId.Value);

        return new ListResponseModel<ProviderUserBulkResponseModel>(results.Select(r =>
            new ProviderUserBulkResponseModel(r.Item1.Id, r.Item2)));
    }

    [HttpPost("public-keys")]
    [Authorize<ManageProviderUsersRequirement>]
    public async Task<ListResponseModel<ProviderUserPublicKeyResponseModel>> UserPublicKeys(Guid providerId, [FromBody] ProviderUserBulkRequestModel model)
    {
        var result = await _providerUserRepository.GetManyPublicKeysByProviderUserAsync(providerId, model.Ids);
        var responses = result.Select(r => new ProviderUserPublicKeyResponseModel(r.Id, r.UserId, r.PublicKey)).ToList();
        return new ListResponseModel<ProviderUserPublicKeyResponseModel>(responses);
    }

    [HttpPut("{id:guid}")]
    [Authorize<ManageProviderUsersRequirement>]
    public async Task Put(Guid providerId, Guid id, [FromBody] ProviderUserUpdateRequestModel model)
    {
        var providerUser = await _providerUserRepository.GetByIdAsync(id);
        if (providerUser == null || providerUser.ProviderId != providerId)
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User);
        await _providerService.SaveUserAsync(model.ToProviderUser(providerUser), userId.Value);
    }

    [HttpPost("{id:guid}")]
    [Obsolete("This endpoint is deprecated. Use PUT method instead")]
    [Authorize<ManageProviderUsersRequirement>]
    public async Task PostPut(Guid providerId, Guid id, [FromBody] ProviderUserUpdateRequestModel model)
    {
        await Put(providerId, id, model);
    }

    [HttpDelete("{id:guid}")]
    [Authorize<ManageProviderUsersRequirement>]
    public async Task Delete(Guid providerId, Guid id)
    {
        var userId = _userService.GetProperUserId(User);
        await _providerService.DeleteUsersAsync(providerId, new[] { id }, userId.Value);
    }

    [HttpPost("{id:guid}/delete")]
    [Obsolete("This endpoint is deprecated. Use DELETE method instead")]
    [Authorize<ManageProviderUsersRequirement>]
    public async Task PostDelete(Guid providerId, Guid id)
    {
        await Delete(providerId, id);
    }

    [HttpDelete("")]
    [Authorize<ManageProviderUsersRequirement>]
    public async Task<ListResponseModel<ProviderUserBulkResponseModel>> BulkDelete(Guid providerId, [FromBody] ProviderUserBulkRequestModel model)
    {
        var userId = _userService.GetProperUserId(User);
        var result = await _providerService.DeleteUsersAsync(providerId, model.Ids, userId.Value);
        return new ListResponseModel<ProviderUserBulkResponseModel>(result.Select(r =>
            new ProviderUserBulkResponseModel(r.Item1.Id, r.Item2)));
    }

    [HttpPost("delete")]
    [Obsolete("This endpoint is deprecated. Use DELETE method instead")]
    [Authorize<ManageProviderUsersRequirement>]
    public async Task<ListResponseModel<ProviderUserBulkResponseModel>> PostBulkDelete(Guid providerId, [FromBody] ProviderUserBulkRequestModel model)
    {
        return await BulkDelete(providerId, model);
    }
}
