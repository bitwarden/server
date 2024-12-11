﻿using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Bit.Admin.TagHelpers;

[HtmlTargetElement("option", Attributes = SelectedName)]
public class OptionSelectedTagHelper : TagHelper
{
    private const string SelectedName = "asp-selected";

    private readonly IHtmlGenerator _generator;

    public OptionSelectedTagHelper(IHtmlGenerator generator)
    {
        _generator = generator;
    }

    [HtmlAttributeName(SelectedName)]
    public bool Selected { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        ArgumentNullException.ThrowIfNull(context);

        ArgumentNullException.ThrowIfNull(output);

        if (Selected)
        {
            output.Attributes.Add("selected", "selected");
        }
        else
        {
            output.Attributes.RemoveAll("selected");
        }
    }
}
