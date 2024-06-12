using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Api.SecretsManager.Models.Response;
using Bit.Commercial.Core.SecretsManager.Commands.Projects;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Commands.Trash.Interfaces;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.SecretsManager.Controllers;

[Authorize("secrets")]
public class RequestSMAccessController : Controller
{
    private readonly IRequestSMAccessCommand _requestSMAccessCommand;
    private readonly IUserService _userService;

    public RequestSMAccessController(
        IRequestSMAccessCommand requestSMAccessCommand, IUserService userService)
    {
        _requestSMAccessCommand = requestSMAccessCommand;
        _userService = userService;
    }

    [HttpPost("request-sm-access")]
    public async Task RequestSMAccessFromAdmins([FromBody] RequestSMAccessRequestModel model)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        await _requestSMAccessCommand.SendRequestAccessToSM(Guid.Parse(model.OrganizationId), user, model.EmailContent);
    }
}
