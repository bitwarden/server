using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Authorization;
using Bit.Core.Models.Api;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.Core.Settings;
using Bit.Core.Models.Api.Response;
using Bit.Core.Enums;
using Microsoft.Azure.EventGrid.Models;
using Bit.Api.Utilities;
using System.Collections.Generic;

namespace Bit.Api.Controllers
{
    [Route("sends")]
    [Authorize("Application")]
    public class SendsController : Controller
    {
        private readonly ISendRepository _sendRepository;
        private readonly IUserService _userService;
        private readonly ISendService _sendService;
        private readonly ISendFileStorageService _sendFileStorageService;
        private readonly GlobalSettings _globalSettings;

        public SendsController(
            ISendRepository sendRepository,
            IUserService userService,
            ISendService sendService,
            ISendFileStorageService sendFileStorageService,
            GlobalSettings globalSettings)
        {
            _sendRepository = sendRepository;
            _userService = userService;
            _sendService = sendService;
            _sendFileStorageService = sendFileStorageService;
            _globalSettings = globalSettings;
        }

        [AllowAnonymous]
        [HttpPost("access/{id}")]
        public async Task<IActionResult> Access(string id, [FromBody] SendAccessRequestModel model)
        {
            var guid = new Guid(CoreHelpers.Base64UrlDecode(id));
            var (send, passwordRequired, passwordInvalid) =
                await _sendService.AccessAsync(guid, model.Password);
            if (passwordRequired)
            {
                return new UnauthorizedResult();
            }
            if (passwordInvalid)
            {
                await Task.Delay(2000);
                throw new BadRequestException("Invalid password.");
            }
            if (send == null)
            {
                throw new NotFoundException();
            }

            var sendResponse = new SendAccessResponseModel(send, _globalSettings);
            if (send.UserId.HasValue)
            {
                var creator = await _userService.GetUserByIdAsync(send.UserId.Value);
                sendResponse.CreatorIdentifier = creator.Email;
            }
            return new ObjectResult(sendResponse);
        }

        [AllowAnonymous]
        [HttpPost("{encodedSendId}/access/file/{fileId}")]
        public async Task<IActionResult> GetSendFileDownloadData(string encodedSendId,
            string fileId, [FromBody] SendAccessRequestModel model)
        {
            var sendId = new Guid(CoreHelpers.Base64UrlDecode(encodedSendId));
            var send = await _sendRepository.GetByIdAsync(sendId);

            if (send == null)
            {
                throw new BadRequestException("Could not locate send");
            }

            var (url, passwordRequired, passwordInvalid) = await _sendService.GetSendFileDownloadUrlAsync(send, fileId,
                model.Password);

            if (passwordRequired)
            {
                return new UnauthorizedResult();
            }
            if (passwordInvalid)
            {
                await Task.Delay(2000);
                throw new BadRequestException("Invalid password.");
            }
            if (send == null)
            {
                throw new NotFoundException();
            }

            return new ObjectResult(new SendFileDownloadDataResponseModel()
            {
                Id = fileId,
                Url = url,
            });
        }

        [HttpGet("{id}")]
        public async Task<SendResponseModel> Get(string id)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var send = await _sendRepository.GetByIdAsync(new Guid(id));
            if (send == null || send.UserId != userId)
            {
                throw new NotFoundException();
            }

            return new SendResponseModel(send, _globalSettings);
        }

        [HttpGet("")]
        public async Task<ListResponseModel<SendResponseModel>> Get()
        {
            var userId = _userService.GetProperUserId(User).Value;
            var sends = await _sendRepository.GetManyByUserIdAsync(userId);
            var responses = sends.Select(s => new SendResponseModel(s, _globalSettings));
            return new ListResponseModel<SendResponseModel>(responses);
        }

        [HttpPost("")]
        public async Task<SendResponseModel> Post([FromBody] SendRequestModel model)
        {
            model.ValidateCreation();
            var userId = _userService.GetProperUserId(User).Value;
            var send = model.ToSend(userId, _sendService);
            await _sendService.SaveSendAsync(send);
            return new SendResponseModel(send, _globalSettings);
        }

        [HttpPost("file")]
        public async Task<SendFileUploadDataResponseModel> PostFile([FromBody] SendRequestModel model)
        {
            if (model.Type != SendType.File)
            {
                throw new BadRequestException("Invalid content.");
            }

            if (!model.FileLength.HasValue)
            {
                throw new BadRequestException("Invalid content. File size hint is required.");
            }

            var userId = _userService.GetProperUserId(User).Value;
            var (send, data) = model.ToSend(userId, model.File.FileName, _sendService);
            var uploadUrl = await _sendService.SaveFileSendAsync(send, data, model.FileLength.Value);
            return new SendFileUploadDataResponseModel
            {
                Url = uploadUrl,
                FileUploadType = _sendFileStorageService.FileUploadType,
                SendResponse = new SendResponseModel(send, _globalSettings)
            };
        }

        [HttpPost("{id}/file/{fileId}")]
        [DisableFormValueModelBinding]
        public async Task PostFileForExistingSend(string id, string fileId)
        {
            if (!Request?.ContentType.Contains("multipart/") ?? true)
            {
                throw new BadRequestException("Invalid content.");
            }

            if (Request.ContentLength > 105906176) // 101 MB, give em' 1 extra MB for cushion
            {
                throw new BadRequestException("Max file size for direct upload is 100 MB.");
            }

            var send = await _sendRepository.GetByIdAsync(new Guid(id));
            await Request.GetSendFileAsync(async (stream) =>
            {
                await _sendFileStorageService.UploadNewFileAsync(stream, send, fileId);
            });
        }

        [AllowAnonymous]
        [HttpPost("file/validate/azure")]
        public async Task<OkObjectResult> AzureValidateFile()
        {
            return await ApiHelpers.HandleAzureEvents(Request, new Dictionary<string, Func<EventGridEvent, Task>>
                {
                  {"Microsoft.Storage.BlobCreated", async (eventGridEvent) => {
                      try
                      {
                          var blobName = eventGridEvent.Subject.Split($"{AzureSendFileStorageService.FilesContainerName}/blobs/")[1];
                          var sendId = AzureSendFileStorageService.SendIdFromBlobName(blobName);
                          var send = await _sendRepository.GetByIdAsync(new Guid(sendId));
                          if (send == null)
                          {
                              return;
                          }
                          await _sendService.ValidateSendFile(send);
                      }
                      catch
                      {
                          return;
                      }
                  }}
                });
        }

        [HttpPut("{id}")]
        public async Task<SendResponseModel> Put(string id, [FromBody] SendRequestModel model)
        {
            model.ValidateEdit();
            var userId = _userService.GetProperUserId(User).Value;
            var send = await _sendRepository.GetByIdAsync(new Guid(id));
            if (send == null || send.UserId != userId)
            {
                throw new NotFoundException();
            }

            await _sendService.SaveSendAsync(model.ToSend(send, _sendService));
            return new SendResponseModel(send, _globalSettings);
        }

        [HttpPut("{id}/remove-password")]
        public async Task<SendResponseModel> PutRemovePassword(string id)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var send = await _sendRepository.GetByIdAsync(new Guid(id));
            if (send == null || send.UserId != userId)
            {
                throw new NotFoundException();
            }

            send.Password = null;
            await _sendService.SaveSendAsync(send);
            return new SendResponseModel(send, _globalSettings);
        }

        [HttpDelete("{id}")]
        public async Task Delete(string id)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var send = await _sendRepository.GetByIdAsync(new Guid(id));
            if (send == null || send.UserId != userId)
            {
                throw new NotFoundException();
            }

            await _sendService.DeleteSendAsync(send);
        }
    }
}
