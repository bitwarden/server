using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Net.Http;
using System.Security.Cryptography;
using Bit.Core.Services;
using Bit.Core;
using System.Net;
using Bit.Core.Exceptions;

namespace Bit.Api.Controllers
{
    [Route("hibp")]
    [Authorize("Application")]
    public class HibpController : Controller
    {
        private const string HibpBreachApi = "https://haveibeenpwned.com/api/v2/breachedaccount/{0}";
        private static HttpClient _httpClient;

        private readonly IUserService _userService;
        private readonly CurrentContext _currentContext;
        private readonly GlobalSettings _globalSettings;

        static HibpController()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Bitwarden");
        }

        public HibpController(
            IUserService userService,
            CurrentContext currentContext,
            GlobalSettings globalSettings)
        {
            _userService = userService;
            _currentContext = currentContext;
            _globalSettings = globalSettings;
        }

        [HttpGet("breach")]
        public async Task<IActionResult> Get(string username)
        {
            var encodedUsername = WebUtility.UrlEncode(username);
            var request = new HttpRequestMessage(HttpMethod.Get, string.Format(HibpBreachApi, encodedUsername));
            if(!string.IsNullOrWhiteSpace(_globalSettings.HibpBreachApiKey))
            {
                request.Headers.Add("Authorization", $"Basic {_globalSettings.HibpBreachApiKey}");
            }
            request.Headers.Add("Client-Id", GetClientId());
            request.Headers.Add("Client-Ip", _currentContext.IpAddress);
            var response = await _httpClient.SendAsync(request);
            if(response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadAsStringAsync();
                return Content(data, "application/json");
            }
            else if(response.StatusCode == HttpStatusCode.NotFound)
            {
                return new NotFoundResult();
            }
            else
            {
                throw new BadRequestException("Request failed. Status code: " + response.StatusCode);
            }
        }

        private string GetClientId()
        {
            var userId = _userService.GetProperUserId(User).Value;
            using(var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(userId.ToByteArray());
                return Convert.ToBase64String(hash);
            }
        }
    }
}
