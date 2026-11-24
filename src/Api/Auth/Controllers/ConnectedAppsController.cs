#nullable enable

using Bit.Api.Auth.Models.Request;
using Bit.Api.Auth.Models.Response;
using Bit.Api.Models.Response;
using Bit.Core;
using Bit.Core.Auth.ConnectedApps.Commands.Interfaces;
using Bit.Core.Auth.Repositories;
using Bit.Core.Services;
using Bitwarden.Server.Sdk.Features;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Auth.Controllers;

/// <summary>
/// Manages a user's agent-access connections — the user-key-encrypted PSK records created during the
/// "Login with Bitwarden" partner flow and synced to the user's clients.
/// </summary>
[Route("connected-apps")]
[Authorize("Application")]
[RequireFeature(FeatureFlagKeys.OpenIdConnectProvider)]
public class ConnectedAppsController : Controller
{
    private readonly IConnectedAppRepository _connectedAppRepository;
    private readonly ICreateConnectedAppCommand _createConnectedAppCommand;
    private readonly IRevokeConnectedAppCommand _revokeConnectedAppCommand;
    private readonly IUserService _userService;

    public ConnectedAppsController(
        IConnectedAppRepository connectedAppRepository,
        ICreateConnectedAppCommand createConnectedAppCommand,
        IRevokeConnectedAppCommand revokeConnectedAppCommand,
        IUserService userService)
    {
        _connectedAppRepository = connectedAppRepository;
        _createConnectedAppCommand = createConnectedAppCommand;
        _revokeConnectedAppCommand = revokeConnectedAppCommand;
        _userService = userService;
    }

    [HttpGet("")]
    public async Task<ListResponseModel<ConnectedAppResponseModel>> Get()
    {
        var userId = _userService.GetProperUserId(User)!.Value;
        var connections = await _connectedAppRepository.GetManyByUserIdAsync(userId);
        return new ListResponseModel<ConnectedAppResponseModel>(
            connections.Select(c => new ConnectedAppResponseModel(c)));
    }

    [HttpPost("")]
    public async Task<ConnectedAppResponseModel> Post([FromBody] ConnectedAppRequestModel model)
    {
        var userId = _userService.GetProperUserId(User)!.Value;
        var connection = await _createConnectedAppCommand.CreateAsync(
            userId, model.PartnerClientId, model.Name, model.Key);
        return new ConnectedAppResponseModel(connection);
    }

    [HttpDelete("{id:guid}")]
    [HttpPost("{id:guid}/delete")]
    public async Task Delete(Guid id)
    {
        var userId = _userService.GetProperUserId(User)!.Value;
        await _revokeConnectedAppCommand.RevokeAsync(userId, id);
    }
}
