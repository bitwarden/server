using System;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Bit.Admin.Models;
using Microsoft.AspNetCore.Authorization;
using Bit.Core;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

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

        public async Task<IActionResult> GetLatestDockerHubVersion(string repository)
        {
            try
            {
                var response = await _httpClient.GetAsync(
                $"https://hub.docker.com/v2/repositories/bitwarden/{repository}/tags/");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var data = JObject.Parse(json);
                    var results = data["results"] as JArray;
                    foreach (var result in results)
                    {
                        var name = result["name"].ToString();
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

        public async Task<IActionResult> GetInstalledWebVersion()
        {
            try
            {
                var response = await _httpClient.GetAsync(
                    $"{_globalSettings.BaseServiceUri.InternalVault}/version.json");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var data = JObject.Parse(json);
                    return new JsonResult(data["version"].ToString());
                }
            }
            catch (HttpRequestException) { }

            return new JsonResult("-");
        }
    }
}
