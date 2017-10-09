using System;
using System.Net.Http;
using System.Threading.Tasks;
using Bit.Icons.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace Bit.Icons.Controllers
{
    [Route("")]
    public class IconController : Controller
    {
        private readonly IMemoryCache _memoryCache;

        public IconController(IMemoryCache memoryCache)
        {
            _memoryCache = memoryCache;
        }

        [HttpGet("")]
        public async Task<IActionResult> Get([FromQuery] string domain)
        {
            if(!domain.StartsWith("http://") || !domain.StartsWith("https://"))
            {
                domain = "http://" + domain;
            }

            if(!Uri.TryCreate(domain, UriKind.Absolute, out Uri uri))
            {
                return new BadRequestResult();
            }

            var iconUrl = BuildIconUrl(uri);
            var icon = await _memoryCache.GetOrCreateAsync(domain, async entry =>
            {
                entry.AbsoluteExpiration = DateTime.Now.AddDays(1);

                var httpClient = new HttpClient();
                var response = await httpClient.GetAsync(iconUrl);
                if(!response.IsSuccessStatusCode)
                {
                    return null;
                }

                return new Icon
                {
                    Image = await response.Content.ReadAsByteArrayAsync(),
                    Format = response.Content.Headers.ContentType.MediaType
                };
            });

            if(icon == null)
            {
                return new NotFoundResult();
            }

            return new FileContentResult(icon.Image, icon.Format);
        }

        private static string BuildIconUrl(Uri uri)
        {
            return $"https://icons.bitwarden.com/icon?url={uri.Host}&size=16..24..200";
        }
    }
}
