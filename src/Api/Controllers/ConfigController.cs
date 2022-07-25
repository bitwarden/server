using Bit.Api.Models.Response;
using Bit.Core.Settings;
using Bit.Core.Utilities;

using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers
{
    [Route("config")]
    public class ConfigController : Controller
    {
        private readonly IGlobalSettings _globalSettings;

        public ConfigController(IGlobalSettings globalSettings)
        {
            _globalSettings = globalSettings;
        }

        [HttpGet("")]
        public ConfigResponseModel GetConfigs()
        {
            ConfigResponseModel response = new ConfigResponseModel();

            var gitHash = AssemblyHelpers.GetGitHash();

            if (!string.IsNullOrWhiteSpace(gitHash))
            {
                response.GitHash = gitHash;
            }

            response.Environment.Api = _globalSettings.BaseServiceUri.Api;

            return response;
        }
    }
}
