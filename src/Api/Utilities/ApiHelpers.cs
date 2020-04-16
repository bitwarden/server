using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System.IO;
using System.Threading.Tasks;

namespace Bit.Api.Utilities
{
    public static class ApiHelpers
    {
        public async static Task<T> ReadJsonFileFromBody<T>(HttpContext httpContext, IFormFile file, long maxSize = 51200)
        {
            T obj = default(T);
            if (file != null && httpContext.Request.ContentLength.HasValue && httpContext.Request.ContentLength.Value <= maxSize)
            {
                try
                {
                    using (var stream = file.OpenReadStream())
                    using (var reader = new StreamReader(stream))
                    {
                        var s = await reader.ReadToEndAsync();
                        if (!string.IsNullOrWhiteSpace(s))
                        {
                            obj = JsonConvert.DeserializeObject<T>(s);
                        }
                    }
                }
                catch { }
            }

            return obj;
        }
    }
}
