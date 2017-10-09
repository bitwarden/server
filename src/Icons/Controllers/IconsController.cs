using System;
using System.Net.Http;
using System.Threading.Tasks;
using Bit.Icons.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Bit.Icons.Controllers
{
    [Route("")]
    public class IconsController : Controller
    {
        private readonly IMemoryCache _memoryCache;
        private readonly IconsSettings _iconsSettings;

        public IconsController(
            IMemoryCache memoryCache,
            IOptions<IconsSettings> iconsSettingsOptions)
        {
            _memoryCache = memoryCache;
            _iconsSettings = iconsSettingsOptions.Value;
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

        private string BuildIconUrl(Uri uri)
        {
            return $"{_iconsSettings.BestIconBaseUrl}/icon?url={uri.Host}&size=16..24..200";
        }
    }
}
