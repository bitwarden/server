using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Authorization;
using Bit.Api.Models;
using Bit.Core.Exceptions;
using Bit.Core.Domains;
using Microsoft.AspNetCore.Identity;

namespace Bit.Api.Controllers
{
    [Route("devices")]
    [Authorize("Application")]
    public class DevicesController : Controller
    {
        private readonly IDeviceRepository _deviceRepository;
        private readonly UserManager<User> _userManager;

        public DevicesController(
            IDeviceRepository deviceRepository,
            UserManager<User> userManager)
        {
            _deviceRepository = deviceRepository;
            _userManager = userManager;
        }

        [HttpGet("{id}")]
        public async Task<DeviceResponseModel> Get(string id)
        {
            var device = await _deviceRepository.GetByIdAsync(new Guid(id), new Guid(_userManager.GetUserId(User)));
            if(device == null)
            {
                throw new NotFoundException();
            }

            var response = new DeviceResponseModel(device);
            return response;
        }

        [HttpGet("identifier/{identifier}")]
        public async Task<DeviceResponseModel> GetByIdentifier(string identifier)
        {
            var device = await _deviceRepository.GetByIdentifierAsync(identifier, new Guid(_userManager.GetUserId(User)));
            if(device == null)
            {
                throw new NotFoundException();
            }

            var response = new DeviceResponseModel(device);
            return response;
        }

        [HttpGet("")]
        public async Task<ListResponseModel<DeviceResponseModel>> Get()
        {
            ICollection<Device> devices = await _deviceRepository.GetManyByUserIdAsync(new Guid(_userManager.GetUserId(User)));
            var responses = devices.Select(d => new DeviceResponseModel(d));
            return new ListResponseModel<DeviceResponseModel>(responses);
        }

        [HttpPost("")]
        public async Task<DeviceResponseModel> Post([FromBody]DeviceRequestModel model)
        {
            var device = model.ToDevice(_userManager.GetUserId(User));
            await _deviceRepository.CreateAsync(device);

            var response = new DeviceResponseModel(device);
            return response;
        }

        [HttpPut("{id}")]
        [HttpPost("{id}")]
        public async Task<DeviceResponseModel> Put(string id, [FromBody]DeviceRequestModel model)
        {
            var device = await _deviceRepository.GetByIdAsync(new Guid(id), new Guid(_userManager.GetUserId(User)));
            if(device == null)
            {
                throw new NotFoundException();
            }

            await _deviceRepository.ReplaceAsync(model.ToDevice(device));

            var response = new DeviceResponseModel(device);
            return response;
        }

        [HttpPut("identifier/{identifier}/token")]
        [HttpPost("identifier/{identifier}/token")]
        public async Task<DeviceResponseModel> PutToken(string identifier, [FromBody]DeviceTokenRequestModel model)
        {
            var device = await _deviceRepository.GetByIdentifierAsync(identifier, new Guid(_userManager.GetUserId(User)));
            if(device == null)
            {
                throw new NotFoundException();
            }

            await _deviceRepository.ReplaceAsync(model.ToDevice(device));

            var response = new DeviceResponseModel(device);
            return response;
        }

        [HttpDelete("{id}")]
        [HttpPost("{id}/delete")]
        public async Task Delete(string id)
        {
            var device = await _deviceRepository.GetByIdAsync(new Guid(id), new Guid(_userManager.GetUserId(User)));
            if(device == null)
            {
                throw new NotFoundException();
            }

            await _deviceRepository.DeleteAsync(device);
        }
    }
}
