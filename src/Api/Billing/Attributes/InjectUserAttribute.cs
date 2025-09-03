#nullable enable
using Bit.Core.Entities;
using Bit.Core.Models.Api;
using Bit.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Bit.Api.Billing.Attributes;

/// <summary>
/// An action filter that facilitates the injection of a <see cref="User"/> parameter into the executing action method arguments.
/// </summary>
/// <remarks>
/// <para>This attribute retrieves the authorized user associated with the current HTTP context using the <see cref="IUserService"/> service.
/// If the user is unauthorized or cannot be found, the request is terminated with an unauthorized response.</para>
/// <para>The injected <see cref="User"/>
/// parameter must be marked with a [BindNever] attribute to short-circuit the model-binding system.</para>
/// </remarks>
/// <example>
/// <code><![CDATA[
/// [HttpPost]
/// [InjectUser]
/// public async Task<IResult> EndpointAsync([BindNever] User user)
/// ]]></code>
/// </example>
/// <seealso cref="ActionFilterAttribute"/>
public class InjectUserAttribute : ActionFilterAttribute
{
    public override async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var userService = context.HttpContext.RequestServices.GetRequiredService<IUserService>();

        var user = await userService.GetUserByPrincipalAsync(context.HttpContext.User);

        if (user == null)
        {
            context.Result = new UnauthorizedObjectResult(new ErrorResponseModel("Unauthorized."));
            return;
        }

        var userParameter =
            context.ActionDescriptor.Parameters.FirstOrDefault(parameter => parameter.ParameterType == typeof(User));

        if (userParameter != null)
        {
            context.ActionArguments[userParameter.Name] = user;
        }

        await next();
    }
}
