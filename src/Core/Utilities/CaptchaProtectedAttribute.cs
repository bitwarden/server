using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Models.Api;
using Bit.Core.Services;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Utilities;

public class CaptchaProtectedAttribute : ActionFilterAttribute
{
    public string ModelParameterName { get; set; } = "model";

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var currentContext = context.HttpContext.RequestServices.GetRequiredService<ICurrentContext>();
        var captchaValidationService = context.HttpContext.RequestServices.GetRequiredService<ICaptchaValidationService>();

        if (captchaValidationService.RequireCaptchaValidation(currentContext, null))
        {
            var captchaResponse = (context.ActionArguments[ModelParameterName] as ICaptchaProtectedModel)?.CaptchaResponse;

            if (string.IsNullOrWhiteSpace(captchaResponse))
            {
                throw new BadRequestException(captchaValidationService.SiteKeyResponseKeyName, captchaValidationService.SiteKey);
            }

            var captchaValidationResponse = captchaValidationService.ValidateCaptchaResponseAsync(captchaResponse,
                currentContext.IpAddress, null).GetAwaiter().GetResult();
            if (!captchaValidationResponse.Success || captchaValidationResponse.IsBot)
            {
                throw new BadRequestException("Captcha is invalid. Please refresh and try again");
            }
        }
    }
}
