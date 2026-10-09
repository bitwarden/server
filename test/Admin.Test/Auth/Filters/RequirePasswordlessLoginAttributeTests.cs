using Bit.Admin;
using Bit.Admin.Auth.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Admin.Test.Auth.Filters;

public class RequirePasswordlessLoginAttributeTests
{
    [Fact]
    public void OnActionExecuting_ShortCircuitsWithNotFound_WhenPasswordlessDisabled()
    {
        var ctx = BuildContext(enablePasswordlessLogin: false);

        new RequirePasswordlessLoginAttribute().OnActionExecuting(ctx);

        Assert.IsType<NotFoundResult>(ctx.Result);
    }

    [Fact]
    public void OnActionExecuting_DoesNotShortCircuit_WhenPasswordlessEnabled()
    {
        var ctx = BuildContext(enablePasswordlessLogin: true);

        new RequirePasswordlessLoginAttribute().OnActionExecuting(ctx);

        Assert.Null(ctx.Result);
    }

    private static ActionExecutingContext BuildContext(bool enablePasswordlessLogin)
    {
        var settings = new AdminSettings { EnablePasswordlessLogin = enablePasswordlessLogin };
        var services = new ServiceCollection();
        services.AddSingleton<IOptions<AdminSettings>>(Options.Create(settings));
        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };

        var actionContext = new ActionContext(httpContext, new RouteData(),
            new ControllerActionDescriptor());
        return new ActionExecutingContext(actionContext, [], new Dictionary<string, object>(),
            controller: new object());
    }
}
