using System.Reflection;
using Bit.Api.Models.Response;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers
{
    [Route("config")]
    public class ConfigController : Controller
    {
        public const string GIT_HASH_ASSEMBLY_KEY = "GitHash";

        public ConfigController()
        {

        }

        [HttpGet("")]
        public ConfigResponseModel GetConfigs()
        {
            ConfigResponseModel response = new ConfigResponseModel();
            var gitHash = Assembly.GetEntryAssembly().GetCustomAttributes<AssemblyMetadataAttribute>();

            if (gitHash.Count() > 0)
            {
                response.GitHash = gitHash.Where(i => i.Key == GIT_HASH_ASSEMBLY_KEY).First().Value;
            }

            return response;
        }
    }
}
