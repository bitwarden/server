using Bit.Admin.Enums;
using Bit.Admin.Services;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Bit.Admin.Utilities;

public class RequirePermissionAttribute : ActionFilterAttribute
{
    public Permission Permission { get; set; }

    public RequirePermissionAttribute(Permission permission)
    {
        Permission = permission;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var accessControlService = context.HttpContext.RequestServices.GetRequiredService<IAccessControlService>();

        var hasPermission = accessControlService.UserHasPermission(Permission);
        if (!hasPermission)
        {
            throw new UnauthorizedAccessException("Not authorized.");
        }
    }
}
