using Bit.Api.Models.Response;
using Bit.Core.Settings;

using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers;

[Route("config")]
public class ConfigController : Controller
{
    private readonly IGlobalSettings _globalSettings;

    public ConfigController(IGlobalSettings globalSettings)
    {
        _globalSettings = globalSettings;
    }

    [HttpGet("")]
    public ConfigResponseModel GetConfigs()
    {
        return new ConfigResponseModel(_globalSettings);
    }
}
