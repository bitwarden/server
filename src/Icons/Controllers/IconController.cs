using System;
using System.IO;
using System.Net.Http;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using Icons.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace Icons.Controllers
{
    [Route("[controller]")]
    public class IconController : Controller
    {
        private readonly IHostingEnvironment _hostingEnvironment;

        public IconController(IHostingEnvironment hostingEnvironment)
        {
            _hostingEnvironment = hostingEnvironment;
        }

        [HttpGet]
        public async Task<FileResult> Get([FromQuery] string domain)
        {
            var uri = BuildUrl(domain);
            var fileName = $"{_hostingEnvironment.ContentRootPath}/cache/{domain}.cache";

            // Attempt to load the icon from the cache.
            if (FileExists(fileName))
            {
                using (Stream stream = System.IO.File.Open(fileName, FileMode.Open))
                {
                    var binaryFormatter = new BinaryFormatter();
                    var icon = (Icon)binaryFormatter.Deserialize(stream);

                    if (icon.HasNotExpired())
                    {
                        return new FileContentResult(icon.Image, icon.Format);
                    }
                }
            }

            var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(uri);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("Cannot load the image");
            }

            // Serialize the icon.
            using (Stream stream = System.IO.File.Open(fileName, FileMode.Create))
            {
                var icon = new Icon(
                    await response.Content.ReadAsByteArrayAsync(),
                    response.Content.Headers.ContentType.MediaType
                );

                var binaryFormatter = new BinaryFormatter();
                binaryFormatter.Serialize(stream, icon);
                return new FileContentResult(icon.Image, icon.Format);
            }
        }

        private static bool FileExists(string fileName)
        {
            return System.IO.File.Exists(fileName);
        }

        private static string BuildUrl(string domain)
        {
            return $"https://icons.bitwarden.com/icon?url={domain}&size=16..24..200";
        }
    }
}
