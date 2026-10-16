// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Api.AdminConsole.Partnerships;
using Bit.Api.AdminConsole.Partnerships.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Bit.Api.Utilities;

public class PublicApiControllersModelConvention : IControllerModelConvention
{
    public void Apply(ControllerModel controller)
    {
        if (controller.ControllerType.IsSubclassOf(typeof(PartnershipControllerBase)))
        {
            // Runs before the error filter's model validation so a request without partnership access never sees a 400.
            controller.Filters.Add(new TypeFilterAttribute(typeof(PartnershipAccessFilter)) { Order = -2 });
            controller.Filters.Add(new TypeFilterAttribute(typeof(PartnershipRateLimitFilter)) { Order = -1 });
            controller.Filters.Add(new PartnershipErrorFilter());
            return;
        }

        var controllerNamespace = controller.ControllerType.Namespace;
        var publicApi = controllerNamespace.Contains(".Public.");
        controller.Filters.Add(new ExceptionHandlerFilterAttribute(publicApi));
        controller.Filters.Add(new ModelStateValidationFilterAttribute(publicApi));
    }
}
