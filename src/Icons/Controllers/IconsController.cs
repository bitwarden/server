using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Bit.Icons.Models;
using Bit.Icons.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace Bit.Icons.Controllers
{
    [Route("")]
    public class IconsController : Controller
    {
        private static readonly HttpClient _httpClient = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        });
        static readonly List<byte[]> _validImageData = new List<byte[]>
        {
            // png
            new byte[] { 137, 80, 78, 71 },
            // ico
            new byte[] { 00, 00, 01, 00 },
            // jpeg
            new byte[] { 255, 216, 255 }
        };
        private readonly IMemoryCache _memoryCache;
        private readonly IDomainMappingService _domainMappingService;
        private readonly IconsSettings _iconsSettings;

        public IconsController(
            IMemoryCache memoryCache,
            IDomainMappingService domainMappingService,
            IconsSettings iconsSettings)
        {
            _memoryCache = memoryCache;
            _domainMappingService = domainMappingService;
            _iconsSettings = iconsSettings;
        }

        [HttpGet("{hostname}/icon.png")]
        [ResponseCache(Duration = 604800 /*7 days*/)]
        public async Task<IActionResult> Get(string hostname)
        {
            if(string.IsNullOrWhiteSpace(hostname) || !hostname.Contains("."))
            {
                return new BadRequestResult();
            }

            var url = $"http://{hostname}";
            if(!Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                return new BadRequestResult();
            }

            var mappedDomain = _domainMappingService.MapDomain(uri.Host);
            if(!_memoryCache.TryGetValue(mappedDomain, out Icon icon))
            {
                var iconUrl = $"{_iconsSettings.BestIconBaseUrl}/icon?url={mappedDomain}&size=16..32..200" +
                    $"&fallback_icon_url=https://raw.githubusercontent.com/bitwarden/web/master/src/images/fa-globe.png";
                var response = await _httpClient.GetAsync(iconUrl);
                response = await FollowRedirectsAsync(response, 1);
                if(!response.IsSuccessStatusCode)
                {
                    return new NotFoundResult();
                }

                var image = await response.Content.ReadAsByteArrayAsync();
                if(!_validImageData.Any(d => d.SequenceEqual(image.Take(d.Length))))
                {
                    return new NotFoundResult();
                }

                icon = new Icon
                {
                    Image = image,
                    Format = response.Content.Headers.ContentType.MediaType
                };

                // Only cache smaller images (<= 50kb)
                if(image.Length <= 50012)
                {
                    _memoryCache.Set(mappedDomain, icon, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = new TimeSpan(_iconsSettings.CacheHours, 0, 0)
                    });
                }
            }

            return new FileContentResult(icon.Image, icon.Format);
        }

        private async Task<HttpResponseMessage> FollowRedirectsAsync(HttpResponseMessage response, int followCount)
        {
            if(response.IsSuccessStatusCode || followCount > 2)
            {
                return response;
            }

            if((response.StatusCode == HttpStatusCode.Redirect || response.StatusCode == HttpStatusCode.MovedPermanently) &&
                response.Headers.Contains("Location"))
            {
                var locationHeader = response.Headers.GetValues("Location").FirstOrDefault();
                if(!string.IsNullOrWhiteSpace(locationHeader) &&
                    Uri.TryCreate(locationHeader, UriKind.Absolute, out Uri location))
                {
                    var message = new HttpRequestMessage
                    {
                        RequestUri = location,
                        Method = HttpMethod.Get
                    };

                    // Let's add some headers to look like we're coming from a web browser request. Some websites
                    // will block our request without these.
                    message.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                        "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/61.0.3163.100 Safari/537.36");
                    message.Headers.Add("Accept-Language", "en-US,en;q=0.8");
                    message.Headers.Add("Cache-Control", "no-cache");
                    message.Headers.Add("Pragma", "no-cache");
                    message.Headers.Add("Accept", "image/webp,image/apng,image/*,*/*;q=0.8");

                    response = await _httpClient.SendAsync(message);
                    response = await FollowRedirectsAsync(response, followCount++);
                }
            }

            return response;
        }
    }
}
