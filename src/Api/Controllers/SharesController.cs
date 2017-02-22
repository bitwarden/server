using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Authorization;
using Bit.Api.Models;
using Bit.Core.Exceptions;
using Bit.Core.Services;

namespace Bit.Api.Controllers
{
    [Route("shares")]
    [Authorize("Application")]
    public class SharesController : Controller
    {
        private readonly IShareRepository _shareRepository;
        private readonly IUserService _userService;

        public SharesController(
            IShareRepository shareRepository,
            IUserService userService)
        {
            _shareRepository = shareRepository;
            _userService = userService;
        }

        [HttpGet("{id}")]
        public async Task<ShareResponseModel> Get(string id)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var share = await _shareRepository.GetByIdAsync(new Guid(id), userId);
            if(share == null)
            {
                throw new NotFoundException();
            }

            return new ShareResponseModel(share);
        }

        [HttpPost("")]
        public async Task<ShareResponseModel> Post([FromBody]ShareRequestModel model)
        {
            var share = model.ToShare(_userService.GetProperUserId(User).Value);
            await _shareRepository.CreateAsync(share);

            var response = new ShareResponseModel(share);
            return response;
        }

        [HttpDelete("{id}")]
        [HttpPost("{id}/delete")]
        public async Task Delete(string id)
        {
            var share = await _shareRepository.GetByIdAsync(new Guid(id), _userService.GetProperUserId(User).Value);
            if(share == null)
            {
                throw new NotFoundException();
            }

            // TODO: permission checks

            await _shareRepository.DeleteAsync(share);
        }
    }
}
