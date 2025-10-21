using System.Collections.Frozen;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Bit.Unified;

public class AssemblyRoutingConvention : IControllerModelConvention
{
    private readonly FrozenDictionary<Assembly, string> _services;

    public AssemblyRoutingConvention(IEnumerable<IApplicationConfigurator> services)
    {
        _services = services.ToFrozenDictionary(s => s.AppAssembly, s => s.RoutePrefix);
    }

    public void Apply(ControllerModel controller)
    {
        if (!_services.TryGetValue(controller.ControllerType.Assembly, out var prefix))
        {
            // TODO: Should we warn?
            return;
        }

        // In case it is fully convention based, insert the prefix route value
        controller.RouteValues.Add("prefix", prefix);

        if (controller.Selectors.Count == 0)
        {
            throw new NotImplementedException("What does this look like.");
        }
        
        foreach (var selector in controller.Selectors)
        {
            if (selector.AttributeRouteModel is not null)
            {
                var template = GetTrimmedOverridePattern(selector.AttributeRouteModel.Template);
                selector.AttributeRouteModel.Template = AttributeRouteModel.CombineTemplates(prefix, template);
            }
            else
            {
                selector.AttributeRouteModel = new AttributeRouteModel(new RouteAttribute(prefix));
            }
        }

        foreach (var actionSelector in controller.Actions.SelectMany(a => a.Selectors))
        {
            if (actionSelector.AttributeRouteModel is null)
            {
                // If it's null let convention and our addition of the controller selector do its work
                continue;
            }

            var template = actionSelector.AttributeRouteModel.Template;

            if (template is null)
            {
                continue;
            }
            else if (template.StartsWith('/'))
            {
                // Add back the override pattern at the beginning
                actionSelector.AttributeRouteModel.Template = $"/{prefix}/{template[1..]}";
            }
            else if (template.StartsWith("~/", StringComparison.Ordinal))
            {
                // TODO: Test this more
                actionSelector.AttributeRouteModel.Template = $"~/{prefix}/{template[2..]}";
            }
        }
    }

    private static string? GetTrimmedOverridePattern(string? template)
    {
        if (template == null)
        {
            return null;
        }
        else if (template.StartsWith('/'))
        {
            return template[1..];
        }
        else if (template.StartsWith("~/", StringComparison.Ordinal))
        {
            return template[2..];
        }

        return template;
    }
}
