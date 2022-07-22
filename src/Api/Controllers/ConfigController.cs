using System.Reflection;
using Bit.Api.Models.Response;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers
{
    [Route("config")]
    public class ConfigController : Controller
    {
        private const string GIT_HASH_ASSEMBLY_KEY = "GitHash";
        private readonly IGlobalSettings _globalSettings;
        private readonly IEnumerable<AssemblyMetadataAttribute> _assemblyMetadataAttributes;

        public ConfigController(IGlobalSettings globalSettings)
        {
            _globalSettings = globalSettings;
            _assemblyMetadataAttributes = Assembly.GetEntryAssembly().GetCustomAttributes<AssemblyMetadataAttribute>();
        }

        [HttpGet("")]
        public ConfigResponseModel GetConfigs()
        {
            ConfigResponseModel response = new ConfigResponseModel();

            if (_assemblyMetadataAttributes.Count() > 0)
            {
                response.GitHash = _assemblyMetadataAttributes.Where(i => i.Key == GIT_HASH_ASSEMBLY_KEY).First().Value;
            }

            response.Environment.Api = _globalSettings.BaseServiceUri.Api;

            return response;
        }
    }
}
