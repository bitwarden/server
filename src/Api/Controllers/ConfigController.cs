using System.Reflection;
using Bit.Api.Models.Response;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers
{
    [Route("config")]
    public class ConfigController : Controller
    {
        private readonly IGlobalSettings _globalSettings;
        private const string GIT_HASH_ASSEMBLY_KEY = "GitHash";

        public ConfigController(IGlobalSettings globalSettings)
        {
            _globalSettings = globalSettings;
        }

        [HttpGet("")]
        public ConfigResponseModel GetConfigs()
        {
            ConfigResponseModel response = new ConfigResponseModel();
            var customAttributes = Assembly.GetEntryAssembly().GetCustomAttributes<AssemblyMetadataAttribute>();

            if (customAttributes.Count() > 0)
            {
                response.GitHash = customAttributes.Where(i => i.Key == GIT_HASH_ASSEMBLY_KEY).First().Value;
            }

            response.Environment.Api = _globalSettings.BaseServiceUri.Api;

            return response;
        }
    }
}
