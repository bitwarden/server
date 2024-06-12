using Bit.Api.SecretsManager.Models.Request;
using Bit.Commercial.Core.SecretsManager.Commands.PasswordManager;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.SecretsManager.Controllers;

[Authorize("secrets")]
[Route("request-access")]
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
