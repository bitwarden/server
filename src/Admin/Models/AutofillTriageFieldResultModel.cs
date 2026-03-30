namespace Bit.Admin.Models;

public class AutofillTriageFieldResultModel
{
    public string? HtmlId { get; set; }
    public string? HtmlName { get; set; }
    public string? HtmlType { get; set; }
    public string? Placeholder { get; set; }
    public string? AriaLabel { get; set; }
    public string? Autocomplete { get; set; }
    public string? FormIndex { get; set; }
    public bool Eligible { get; set; }
    public string? QualifiedAs { get; set; }
    public List<AutofillTriageConditionResultModel> Conditions { get; set; } = [];
}

public class AutofillTriageConditionResultModel
{
    public string? Description { get; set; }
    public bool Passed { get; set; }
}
