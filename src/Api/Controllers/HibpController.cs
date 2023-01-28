using System.Net;
using System.Security.Cryptography;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers;

[Route("hibp")]
[Authorize("Application")]
public class HibpController : Controller
{
    private const string HibpBreachApi = "https://haveibeenpwned.com/api/v3/breachedaccount/{0}" +
        "?truncateResponse=false&includeUnverified=false";
    private static HttpClient _httpClient;

    private readonly IUserService _userService;
    private readonly ICurrentContext _currentContext;
    private readonly GlobalSettings _globalSettings;
    private readonly string _userAgent;

    static HibpController()
    {
        _httpClient = new HttpClient();
    }

    public HibpController(
        IUserService userService,
        ICurrentContext currentContext,
        GlobalSettings globalSettings)
    {
        _userService = userService;
        _currentContext = currentContext;
        _globalSettings = globalSettings;
        _userAgent = _globalSettings.SelfHosted ? "Bitwarden Self-Hosted" : "Bitwarden";
    }

    [HttpGet("breach")]
    public async Task<IActionResult> Get(string username)
    {
        return await SendAsync(WebUtility.UrlEncode(username), true);
    }

    private async Task<IActionResult> SendAsync(string username, bool retry)
    {
        if (!CoreHelpers.SettingHasValue(_globalSettings.HibpApiKey))
        {
            throw new BadRequestException("HaveIBeenPwned API key not set.");
        }
        var request = new HttpRequestMessage(HttpMethod.Get, string.Format(HibpBreachApi, username));
        request.Headers.Add("hibp-api-key", _globalSettings.HibpApiKey);
        request.Headers.Add("hibp-client-id", GetClientId());
        request.Headers.Add("User-Agent", _userAgent);
        var response = await _httpClient.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            var data = await response.Content.ReadAsStringAsync();
            return Content(data, "application/json");
        }
        else if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new NotFoundResult();
        }
        else if (response.StatusCode == HttpStatusCode.TooManyRequests && retry)
        {
            var delay = 2000;
            if (response.Headers.Contains("retry-after"))
            {
                var vals = response.Headers.GetValues("retry-after");
                if (vals.Any() && int.TryParse(vals.FirstOrDefault(), out var secDelay))
                {
                    delay = (secDelay * 1000) + 200;
                }
            }
            await Task.Delay(delay);
            return await SendAsync(username, false);
        }
        else
        {
            throw new BadRequestException("Request failed. Status code: " + response.StatusCode);
        }
    }

    private string GetClientId()
    {
        var userId = _userService.GetProperUserId(User).Value;
        using (var sha256 = SHA256.Create())
        {
            var hash = sha256.ComputeHash(userId.ToByteArray());
            return Convert.ToBase64String(hash);
        }
    }
}
