using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Bit.Core.Identity;
using Bit.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Bit.Core.Exceptions;
using Bit.Core.Services;

namespace Bit.Api.Controllers
{
    [Obsolete]
    [Route("auth")]
    public class AuthController : Controller
    {
        private readonly JwtBearerSignInManager _signInManager;
        private readonly IUserService _userService;

        public AuthController(
            JwtBearerSignInManager signInManager,
            IUserService userService)
        {
            _signInManager = signInManager;
            _userService = userService;
        }

        [HttpPost("token")]
        [AllowAnonymous]
        public async Task<AuthTokenResponseModel> PostToken([FromBody]AuthTokenRequestModel model)
        {
            var result = await _signInManager.PasswordSignInAsync(model.Email.ToLower(), model.MasterPasswordHash, 
                model.Device?.ToDevice());
            if(result == JwtBearerSignInResult.Success)
            {
                return new AuthTokenResponseModel(result.Token, result.User);
            }
            else if(result == JwtBearerSignInResult.TwoFactorRequired)
            {
                return new AuthTokenResponseModel(result.Token, null);
            }

            await Task.Delay(2000);
            throw new BadRequestException("Username or password is incorrect. Try again.");
        }

        [HttpPost("token/two-factor")]
        [Authorize("TwoFactor")]
        public async Task<AuthTokenResponseModel> PostTokenTwoFactor([FromBody]AuthTokenTwoFactorRequestModel model)
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            var result = await _signInManager.TwoFactorSignInAsync(user, model.Provider, model.Code, model.Device?.ToDevice());
            if(result == JwtBearerSignInResult.Success)
            {
                return new AuthTokenResponseModel(result.Token, result.User);
            }

            await Task.Delay(2000);
            throw new BadRequestException("Code is not correct. Try again.");
        }
    }
}
