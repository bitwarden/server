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

            response.GitHash = Assembly.GetEntryAssembly().GetCustomAttributes<AssemblyMetadataAttribute>().Where(i => i.Key == GIT_HASH_ASSEMBLY_KEY).First().Value;

            return response;
        }
    }
}

/*

Plan...
- Make ConfigResponseModel
- The response model should be based on an obj, maybe an entity?
  Find out what kind if possible
- What is the procedure for error handling?
  - Ex: DB connection cannot be made?

- Questions:
  - ConfigResponseModel w/ multiple classes?
  - why is the ex "500MB" a string? Why not int w/ MB as standard?
    - (or a struct with two fields like value and unit?)

*/