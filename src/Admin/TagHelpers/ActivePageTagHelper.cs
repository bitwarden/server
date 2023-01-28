using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Bit.Admin.TagHelpers;

[HtmlTargetElement("li", Attributes = ActiveControllerName)]
[HtmlTargetElement("li", Attributes = ActiveActionName)]
public class ActivePageTagHelper : TagHelper
{
    private const string ActiveControllerName = "active-controller";
    private const string ActiveActionName = "active-action";

    private readonly IHtmlGenerator _generator;

    public ActivePageTagHelper(IHtmlGenerator generator)
    {
        _generator = generator;
    }

    [HtmlAttributeNotBound]
    [ViewContext]
    public ViewContext ViewContext { get; set; }
    [HtmlAttributeName(ActiveControllerName)]
    public string ActiveController { get; set; }
    [HtmlAttributeName(ActiveActionName)]
    public string ActiveAction { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (output == null)
        {
            throw new ArgumentNullException(nameof(output));
        }

        if (ActiveAction == null && ActiveController == null)
        {
            return;
        }

        var descriptor = ViewContext.ActionDescriptor as ControllerActionDescriptor;
        if (descriptor == null)
        {
            return;
        }

        var controllerMatch = ActiveMatch(ActiveController, descriptor.ControllerName);
        var actionMatch = ActiveMatch(ActiveAction, descriptor.ActionName);
        if (controllerMatch && actionMatch)
        {
            var classValue = "active";
            if (output.Attributes["class"] != null)
            {
                classValue += " " + output.Attributes["class"].Value;
                output.Attributes.Remove(output.Attributes["class"]);
            }

            output.Attributes.Add("class", classValue);
        }
    }

    private bool ActiveMatch(string route, string descriptor)
    {
        return route == null || route == "*" ||
            route.Split(',').Any(c => c.Trim().ToLower() == descriptor.ToLower());
    }
}
