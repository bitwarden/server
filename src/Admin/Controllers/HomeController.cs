using System.Diagnostics;
using System.Text.Json;
using Bit.Admin.Models;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace Bit.Admin.Controllers;

public class HomeController : Controller
{
    private readonly GlobalSettings _globalSettings;
    private readonly HttpClient _httpClient = new HttpClient();
    private readonly ILogger<HomeController> _logger;

    public HomeController(GlobalSettings globalSettings, ILogger<HomeController> logger)
    {
        _globalSettings = globalSettings;
        _logger = logger;
    }

    [Authorize]
    public IActionResult Index()
    {
        return View(new HomeModel
        {
            GlobalSettings = _globalSettings,
            CurrentVersion = Core.Utilities.AssemblyHelpers.GetVersion()
        });
    }

    public IActionResult Error()
    {
        return View(new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
        });
    }


    public async Task<IActionResult> GetLatestVersion(ProjectType project, CancellationToken cancellationToken)
    {
        var requestUri = $"https://selfhost.bitwarden.com/version.json";
        try
        {
            var response = await _httpClient.GetAsync(requestUri, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var latestVersions = JsonConvert.DeserializeObject<LatestVersions>(await response.Content.ReadAsStringAsync());
                return project switch
                {
                    ProjectType.Core => new JsonResult(latestVersions.Versions.CoreVersion),
                    ProjectType.Web => new JsonResult(latestVersions.Versions.WebVersion),
                    _ => throw new System.NotImplementedException(),
                };
            }
        }
        catch (HttpRequestException e)
        {
            _logger.LogError(e, $"Error encountered while sending GET request to {requestUri}");
            return new JsonResult("Unable to fetch latest version") { StatusCode = StatusCodes.Status500InternalServerError };
        }

        return new JsonResult("-");
    }

    public async Task<IActionResult> GetInstalledWebVersion(CancellationToken cancellationToken)
    {
        var requestUri = $"{_globalSettings.BaseServiceUri.InternalVault}/version.json";
        try
        {
            var response = await _httpClient.GetAsync(requestUri, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                using var jsonDocument = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
                var root = jsonDocument.RootElement;
                return new JsonResult(root.GetProperty("version").GetString());
            }
        }
        catch (HttpRequestException e)
        {
            _logger.LogError(e, $"Error encountered while sending GET request to {requestUri}");
            return new JsonResult("Unable to fetch installed version") { StatusCode = StatusCodes.Status500InternalServerError };
        }

        return new JsonResult("-");
    }

    private class LatestVersions
    {
        [JsonProperty("versions")]
        public Versions Versions { get; set; }
    }

    private class Versions
    {
        [JsonProperty("coreVersion")]
        public string CoreVersion { get; set; }

        [JsonProperty("webVersion")]
        public string WebVersion { get; set; }

        [JsonProperty("keyConnectorVersion")]
        public string KeyConnectorVersion { get; set; }
    }
}

public enum ProjectType
{
    Core,
    Web,
}
