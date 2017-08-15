using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Bit.Core.Repositories;
using Bit.Core.Models.Api;
using Bit.Api.Utilities;
using Microsoft.AspNetCore.Authorization;

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

        [HttpPost("")]
        [AllowAnonymous]
        public async Task<InstallationResponseModel> Post([FromBody] InstallationRequestModel model)
        {
            var installation = model.ToInstallation();
            await _installationRepository.CreateAsync(installation);
            return new InstallationResponseModel(installation);
        }
    }
}
