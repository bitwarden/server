#nullable enable

using Bit.Api.Auth.Models.Request;
using Bit.Api.Auth.Models.Response;
using Bit.Core;
using Bit.Api.Models.Response;
using Bit.Core.Auth.AgentFeatures.Commands.Interfaces;
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
[Route("agent-connections")]
[Authorize("Application")]
[RequireFeature(FeatureFlagKeys.OpenIdConnectProvider)]
public class AgentConnectionsController : Controller
{
    private readonly IAgentConnectionRepository _agentConnectionRepository;
    private readonly ICreateAgentConnectionCommand _createAgentConnectionCommand;
    private readonly IRevokeAgentConnectionCommand _revokeAgentConnectionCommand;
    private readonly IUserService _userService;

    public AgentConnectionsController(
        IAgentConnectionRepository agentConnectionRepository,
        ICreateAgentConnectionCommand createAgentConnectionCommand,
        IRevokeAgentConnectionCommand revokeAgentConnectionCommand,
        IUserService userService)
    {
        _agentConnectionRepository = agentConnectionRepository;
        _createAgentConnectionCommand = createAgentConnectionCommand;
        _revokeAgentConnectionCommand = revokeAgentConnectionCommand;
        _userService = userService;
    }

    [HttpGet("")]
    public async Task<ListResponseModel<AgentConnectionResponseModel>> Get()
    {
        var userId = _userService.GetProperUserId(User)!.Value;
        var connections = await _agentConnectionRepository.GetManyByUserIdAsync(userId);
        return new ListResponseModel<AgentConnectionResponseModel>(
            connections.Select(c => new AgentConnectionResponseModel(c)));
    }

    [HttpPost("")]
    public async Task<AgentConnectionResponseModel> Post([FromBody] AgentConnectionRequestModel model)
    {
        var userId = _userService.GetProperUserId(User)!.Value;
        var connection = await _createAgentConnectionCommand.CreateAsync(
            userId, model.PartnerClientId, model.Name, model.Key);
        return new AgentConnectionResponseModel(connection);
    }

    [HttpDelete("{id:guid}")]
    [HttpPost("{id:guid}/delete")]
    public async Task Delete(Guid id)
    {
        var userId = _userService.GetProperUserId(User)!.Value;
        await _revokeAgentConnectionCommand.RevokeAsync(userId, id);
    }
}
