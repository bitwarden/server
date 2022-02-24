using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Bit.Admin.Models;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Admin.Controllers
{
    public class HomeController : Controller
    {
        private readonly GlobalSettings _globalSettings;
        private HttpClient _httpClient = new HttpClient();

        public HomeController(GlobalSettings globalSettings)
        {
            _globalSettings = globalSettings;
        }

        [Authorize]
        public IActionResult Index()
        {
            return View(new HomeModel
            {
                GlobalSettings = _globalSettings,
                CurrentVersion = Core.Utilities.CoreHelpers.GetVersion()
            });
        }

        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }

        public async Task<IActionResult> GetLatestDockerHubVersion(string repository, CancellationToken cancellationToken)
        {
            try
            {
                var response = await _httpClient.GetAsync(
                $"https://hub.docker.com/v2/repositories/bitwarden/{repository}/tags/", cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    using var jsonDocument = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
                    var root = jsonDocument.RootElement;

                    var results = root.GetProperty("results");
                    foreach (var result in results.EnumerateArray())
                    {
                        var name = result.GetProperty("name").GetString();
                        if (!string.IsNullOrWhiteSpace(name) && name.Length > 0 && char.IsNumber(name[0]))
                        {
                            return new JsonResult(name);
                        }
                    }
                }
            }
            catch (HttpRequestException) { }

            return new JsonResult("-");
        }

        public async Task<IActionResult> GetInstalledWebVersion(CancellationToken cancellationToken)
        {
            try
            {
                var response = await _httpClient.GetAsync(
                    $"{_globalSettings.BaseServiceUri.InternalVault}/version.json", cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    using var jsonDocument = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
                    var root = jsonDocument.RootElement;
                    return new JsonResult(root.GetProperty("version").GetString());
                }
            }
            catch (HttpRequestException) { }

            return new JsonResult("-");
        }
    }
}
