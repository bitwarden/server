using System.Text.RegularExpressions;

public static class TemplateProcessor
{
    private static readonly Regex TokenRegex = new(@"#(\w+)#", RegexOptions.Compiled);

    public static string ReplaceTokens(string template, object values)
    {
        if (string.IsNullOrEmpty(template) || values == null)
            return template;

        var type = values.GetType();
        return TokenRegex.Replace(template, match =>
        {
            var propertyName = match.Groups[1].Value;
            var property = type.GetProperty(propertyName);
            return property?.GetValue(values)?.ToString() ?? match.Value;
        });
    }
}
