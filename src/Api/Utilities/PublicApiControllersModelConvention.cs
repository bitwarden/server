using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Bit.Api.Utilities
{
    public class PublicApiControllersModelConvention : IControllerModelConvention
    {
        public void Apply(ControllerModel controller)
        {
            var controllerNamespace = controller.ControllerType.Namespace;
            if(controllerNamespace.Contains(".Public."))
            {
                controller.Filters.Add(new CamelCaseJsonResultFilterAttribute());
            }
        }
    }
}
