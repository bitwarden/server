using Bit.Api.Tools.Models.Response;
using Bit.Core;
using Bit.Core.Auth.Identity;
using Bit.Core.Auth.UserFeatures.SendAccess;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Tools.SendFeatures.Commands.Interfaces;
using Bit.Core.Tools.SendFeatures.Queries.Interfaces;
using Bit.Core.Tools.SendFeatures.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Tools.Controllers;

[Route("sends/access")]
public class SendsAccessController : Controller
{
    private readonly ISendAccessQuery _sendAccessQuery;
    private readonly IUserService _userService;
    private readonly ISendAccessCommand _sendAccessCommand;
    private readonly IEventService _eventService;
    private readonly ISendEventClassifier _sendEventClassifier;
    private readonly Bitwarden.Server.Sdk.Features.IFeatureService _featureService;

    public SendsAccessController(
        ISendAccessQuery sendAccessQuery,
        IUserService userService,
        ISendAccessCommand sendAccessCommand,
        IEventService eventService,
        ISendEventClassifier sendEventClassifier,
        Bitwarden.Server.Sdk.Features.IFeatureService featureService
    )
    {
        _sendAccessQuery = sendAccessQuery;
        _userService = userService;
        _sendAccessCommand = sendAccessCommand;
        _eventService = eventService;
        _sendEventClassifier = sendEventClassifier;
        _featureService = featureService;
    }

    [Authorize(Policy = Policies.Send)]
    [HttpPost("")]
    [ProducesResponseType<SendAccessResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AccessUsingAuth()
    {
        var guid = User.GetSendId();
        var send = await _sendAccessQuery.Get(guid);
        if (send == null)
        {
            throw new BadRequestException("Could not locate send");
        }

        if (!ISendAccessCommand.SendCanBeAccessed(send) || (send.Type == SendType.Item && !_featureService.IsEnabled(FeatureFlagKeys.TemporaryItemSharing)))
        {
            throw new NotFoundException();
        }

        var sendResponse = new SendAccessResponseModel(send);
        if (send.UserId.HasValue && !send.HideEmail.GetValueOrDefault())
        {
            var creator = await _userService.GetUserByIdAsync(send.UserId.Value);
            sendResponse.CreatorIdentifier = creator.Email;
        }

        /*
         * AccessCount is incremented differently depending on Send type:
         * - Text and Item Sends are incremented at every access
         * - File Sends are incremented only when the file is downloaded
         *
         * Note that this endpoint is initially called for all Send types
         */
        if (send.Type == SendType.Text || send.Type == SendType.Item)
        {
            await _sendAccessCommand.IncrementAccessCountAsync(send);
        }

        if (send.UserId.HasValue && (send.Type == SendType.Text || send.Type == SendType.Item))
        {
            var orgContext = await _sendEventClassifier.BuildAccessContextAsync(
                send.UserId.Value,
                User.GetSendAccessEmail());

            if (send.Type == SendType.Text)
            {
                await _eventService.LogSendEventAsync(send.UserId.Value, send.Id, EventType.Send_Accessed_Text, orgContext);
            }
            else if (send.Type == SendType.Item)
            {
                await _eventService.LogSendEventAsync(send.UserId.Value, send.Id, EventType.Send_Accessed_Item, orgContext);
            }
        }

        return new ObjectResult(sendResponse);
    }

    [Authorize(Policy = Policies.Send)]
    [HttpPost("file/{fileId}")]
    [ProducesResponseType<SendFileDownloadDataResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSendFileDownloadDataUsingAuth([FromRoute] string fileId)
    {
        var sendId = User.GetSendId();
        var send = await _sendAccessQuery.Get(sendId);

        if (send == null)
        {
            throw new BadRequestException("Could not locate send");
        }

        var (url, result) = await _sendAccessCommand.GetSendFileDownloadUrlAsync(send, fileId);

        if (result.Equals(SendAccessResult.Denied))
        {
            throw new NotFoundException();
        }

        if (send.UserId.HasValue)
        {
            var orgContext = await _sendEventClassifier.BuildAccessContextAsync(
                send.UserId.Value,
                User.GetSendAccessEmail());

            await _eventService.LogSendEventAsync(
                send.UserId.Value,
                send.Id,
                EventType.Send_Accessed_File,
                orgContext);
        }

        return new ObjectResult(new SendFileDownloadDataResponseModel() { Id = fileId, Url = url });
    }
}
