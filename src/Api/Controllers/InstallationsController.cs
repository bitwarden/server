using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Bit.Core.Repositories;
using Bit.Core.Models.Api;
using Microsoft.AspNetCore.Authorization;
using Bit.Core.Exceptions;
using Bit.Core.Utilities;

namespace Bit.Api.Controllers
{
    [Route("installations")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public class InstallationsController : Controller
    {
        private readonly IInstallationRepository _installationRepository;

        public InstallationsController(
            IInstallationRepository installationRepository)
        {
            _installationRepository = installationRepository;
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<InstallationResponseModel> Get(Guid id)
        {
            var installation = await _installationRepository.GetByIdAsync(id);
            if (installation == null)
            {
                throw new NotFoundException();
            }

            return new InstallationResponseModel(installation, false);
        }

        [HttpPost("")]
        [AllowAnonymous]
        public async Task<InstallationResponseModel> Post([FromBody] InstallationRequestModel model)
        {
            var installation = model.ToInstallation();
            await _installationRepository.CreateAsync(installation);
            return new InstallationResponseModel(installation, true);
        }
    }
}
