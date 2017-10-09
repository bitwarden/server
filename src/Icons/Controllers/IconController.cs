using System;
using System.Net.Http;
using System.Threading.Tasks;
using Icons.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace Icons.Controllers
{
    [Route("[controller]")]
    public class IconController : Controller
    {
        private readonly IMemoryCache _cache;

        public IconController(IMemoryCache memoryCache)
        {
            this._cache = memoryCache;
        }

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string domain)
        {
            var uri = BuildUrl(domain);

            Icon icon = await _cache.GetOrCreateAsync(domain, async entry =>
            {
                entry.AbsoluteExpiration = DateTime.Now.AddDays(1);

                var httpClient = new HttpClient();
                var response = await httpClient.GetAsync(uri);

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                return new Icon(
                    await response.Content.ReadAsByteArrayAsync(),
                    response.Content.Headers.ContentType.MediaType
                );
            });

            if (icon == null)
            {
                return NotFound("Cannot load the icon.");
            }

            return new FileContentResult(icon.Image, icon.Format);
        }

        private static string BuildUrl(string domain)
        {
            return $"https://icons.bitwarden.com/icon?url={domain}&size=16..24..200";
        }
    }
}
