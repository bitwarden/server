using Bit.SharedWeb.Utilities;

// ReSharper disable once CheckNamespace
namespace Microsoft.AspNetCore.Mvc.Rendering;

public static class HtmlHelper
{
    public static IEnumerable<SelectListItem> GetEnumSelectList<T>(
        this IHtmlHelper htmlHelper,
        IEnumerable<T> values
    )
        where T : Enum
    {
        return values.Select(v => new SelectListItem
        {
            Text = v.GetDisplayAttribute().Name,
            Value = v.ToString(),
        });
    }
}
