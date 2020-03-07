using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Bit.Api.Utilities
{
    public class CamelCaseJsonResultFilterAttribute : IAsyncResultFilter
    {
        private static JsonSerializerSettings _jsonSerializerSettings;

        static CamelCaseJsonResultFilterAttribute()
        {
            _jsonSerializerSettings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
        }

        public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
        {
            if(context.Result is JsonResult jsonResult)
            {
                context.Result = new JsonResult(jsonResult.Value, _jsonSerializerSettings);
            }
            await next();
        }
    }
}
