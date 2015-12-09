using System;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Bit.Core.Identity;
using Bit.Core.Repositories;
using Bit.Api.Models;
using Microsoft.AspNet.Authorization;
using Bit.Core.Exceptions;
using Bit.Core;

namespace Bit.Api.Controllers
{
    [Route("auth")]
    public class AuthController : Controller
    {
        private readonly JwtBearerSignInManager _signInManager;
        private readonly IUserRepository _userRepository;
        private readonly CurrentContext _currentContext;

        public AuthController(
            JwtBearerSignInManager signInManager,
            IUserRepository userRepository,
            CurrentContext currentContext)
        {
            _signInManager = signInManager;
            _userRepository = userRepository;
            _currentContext = currentContext;
        }

        [HttpPost("token")]
        [AllowAnonymous]
        public async Task<AuthTokenResponseModel> PostToken([FromBody]AuthTokenRequestModel model)
        {
            var result = await _signInManager.PasswordSignInAsync(model.Email.ToLower(), model.MasterPasswordHash);
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
            var result = await _signInManager.TwoFactorSignInAsync(_currentContext.User, model.Provider, model.Code);
            if(result == JwtBearerSignInResult.Success)
            {
                return new AuthTokenResponseModel(result.Token, result.User);
            }

            await Task.Delay(2000);
            throw new BadRequestException("Code is not correct. Try again.");
        }
    }
}
