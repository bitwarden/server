using Bit.Core.SecretsManagerFeatures.Ping;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers;

[Route("secrets")]
public class SecretsController
{
    private readonly IMediator _mediator;

    public SecretsController(IMediator mediator)
    {
        _mediator = mediator;
    }
    
    [HttpGet("ping")]
    public async Task<string> Ping()
    {
        var response = await _mediator.Send(new Ping());

        return response;
    }
}
