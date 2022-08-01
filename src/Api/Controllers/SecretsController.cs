using Bit.Core.SecretsManagerFeatures.AltPing.Interfaces;
using Bit.Core.SecretsManagerFeatures.Ping;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers;

[Route("secrets")]
public class SecretsController
{
    private readonly IMediator _mediator;
    private readonly IAltPingCommand _altPingCommand;

    public SecretsController(IMediator mediator, IAltPingCommand altPingCommand)
    {
        _mediator = mediator;
        _altPingCommand = altPingCommand;
    }

    [HttpGet("ping")]
    public async Task<string> Ping()
    {
        var response = await _mediator.Send(new Ping());

        return response;
    }

    [HttpGet("alt-ping")]
    public async Task<string> AltPing()
    {
        var response = await _altPingCommand.Ping();

        return response;
    }
}
