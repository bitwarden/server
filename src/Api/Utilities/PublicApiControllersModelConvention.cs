using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Bit.Api.Utilities
{
    public class PublicApiControllersModelConvention : IControllerModelConvention
    {
        public void Apply(ControllerModel controller)
        {
            var controllerNamespace = controller.ControllerType.Namespace;
            var publicApi = controllerNamespace.Contains(".Public.");
            if(publicApi)
            {
                controller.Filters.Add(new CamelCaseJsonResultFilterAttribute());
            }
            controller.Filters.Add(new ExceptionHandlerFilterAttribute(publicApi));
            controller.Filters.Add(new ModelStateValidationFilterAttribute(publicApi));
        }
    }
}
