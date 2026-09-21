// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Net;
using System.Security.Cryptography;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Dirt.Controllers;

/// <summary>
/// Controller for endpoints that make calls to HaveIBeenPwned APIs. This avoids CORS complications in the browser by acting as a proxy for the client.
/// </summary>
[Route("hibp")]
[Authorize("Application")]
public class HibpController : Controller
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IUserService _userService;
    private readonly GlobalSettings _globalSettings;

    public HibpController(
        IHttpClientFactory httpClientFactory,
        IUserService userService,
        GlobalSettings globalSettings)
    {
        _httpClientFactory = httpClientFactory;
        _userService = userService;
        _globalSettings = globalSettings;
    }

    // <summary>
    // Forwards call to the HaveIBeenPwned Pwned passwords API for the supplied hash prefix and returns the response. 
    // Documentation for the endpoint available at: https://haveibeenpwned.com/API/V3#PwnedPasswords
    // </summary>
    [HttpGet("range/{hash:length(5)}")]
    public async Task<IActionResult> GetRangeAsync(string hash)
    {
        var httpClient = _httpClientFactory.CreateClient();

        var url = $"https://api.pwnedpasswords.com/range/{hash}";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent", _globalSettings.SelfHosted ? "Bitwarden Self-Hosted" : "Bitwarden");
        request.Headers.Add("Add-Padding", "true"); // enables padding in response to further protect privacy

        var response = await httpClient.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            var data = await response.Content.ReadAsStringAsync();
            return Content(data, "text/plain");
        }
        else
        {
            throw new BadRequestException("Request failed. Status code: " + response.StatusCode);
        }
    }

    // <summary>
    // Forwards call to the HaveIBeenPwned Breached Accounts API for the supplied username and returns the response. 
    // Documentation for the endpoint available at: https://haveibeenpwned.com/API/V3#BreachesForAccount
    [HttpGet("breach")]
    public async Task<IActionResult> GetBreachAsync(string username)
    {
        return await SendAsync(WebUtility.UrlEncode(username), true);
    }

    private async Task<IActionResult> SendAsync(string username, bool retry)
    {
        if (!CoreHelpers.SettingHasValue(_globalSettings.HibpApiKey))
        {
            throw new BadRequestException("HaveIBeenPwned API key not set.");
        }

        var httpClient = _httpClientFactory.CreateClient();

        var url = $"https://haveibeenpwned.com/api/v3/breachedaccount/{username}?truncateResponse=false&includeUnverified=false";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("hibp-api-key", _globalSettings.HibpApiKey);
        request.Headers.Add("hibp-client-id", GetClientId());
        request.Headers.Add("User-Agent", _globalSettings.SelfHosted ? "Bitwarden Self-Hosted" : "Bitwarden");

        var response = await httpClient.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            var data = await response.Content.ReadAsStringAsync();
            return Content(data, "application/json");
        }
        else if (response.StatusCode == HttpStatusCode.NotFound)
        {
            /* 12/1/2025 - Per the HIBP API, If the domain does not have any email addresses in any breaches, 
               an HTTP 404 response will be returned. API also specifies that "404 Not found is the account could 
               not be found and has therefore not been pwned". Per REST semantics we will return 200 OK with empty array. */
            return Content("[]", "application/json");
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
