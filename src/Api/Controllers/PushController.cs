using Microsoft.AspNetCore.Mvc;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Bit.Core;
using Bit.Core.Exceptions;
using Bit.Core.Models.Api;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNetCore.Hosting;

namespace Bit.Api.Controllers
{
    [Route("push")]
    [Authorize("Push")]
    public class PushController : Controller
    {
        private readonly IPushRegistrationService _pushRegistrationService;
        private readonly IHostingEnvironment _environment;
        private readonly CurrentContext _currentContext;
        private readonly GlobalSettings _globalSettings;

        public PushController(
            IPushRegistrationService pushRegistrationService,
            IHostingEnvironment environment,
            CurrentContext currentContext,
            GlobalSettings globalSettings)
        {
            _currentContext = currentContext;
            _environment = environment;
            _pushRegistrationService = pushRegistrationService;
            _globalSettings = globalSettings;
        }

        [HttpPost("register")]
        public async Task PostRegister(PushRegistrationRequestModel model)
        {
            CheckUsage();
            await _pushRegistrationService.CreateOrUpdateRegistrationAsync(model.PushToken, Prefix(model.DeviceId),
                Prefix(model.UserId), Prefix(model.Identifier), model.Type);
        }

        [HttpDelete("{id}")]
        public async Task Delete(string id)
        {
            CheckUsage();
            await _pushRegistrationService.DeleteRegistrationAsync(Prefix(id));
        }

        [HttpPut("add-organization")]
        public async Task PutAddOrganization(PushUpdateRequestModel model)
        {
            CheckUsage();
            await _pushRegistrationService.AddUserRegistrationOrganizationAsync(
                model.DeviceIds.Select(d => Prefix(d)), Prefix(model.OrganizationId));
        }

        [HttpPut("delete-organization")]
        public async Task PutDeleteOrganization(PushUpdateRequestModel model)
        {
            CheckUsage();
            await _pushRegistrationService.DeleteUserRegistrationOrganizationAsync(
                model.DeviceIds.Select(d => Prefix(d)), Prefix(model.OrganizationId));
        }

        private string Prefix(string value)
        {
            return $"{_currentContext.InstallationId.Value}_{value}";
        }

        private void CheckUsage()
        {
            if(CanUse())
            {
                return;
            }

            throw new BadRequestException("Not correctly configured for push relays.");
        }

        private bool CanUse()
        {
            if(_environment.IsDevelopment())
            {
                return true;
            }

            return _currentContext.InstallationId.HasValue && _globalSettings.SelfHosted;
        }
    }
}
