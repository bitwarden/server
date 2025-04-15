using System.Text.RegularExpressions;

namespace Bit.Core.AdminConsole.Utilities;

public static partial class TemplateProcessor
{
    [GeneratedRegex(@"#(\w+)#")]
    private static partial Regex TokenRegex();

    public static string ReplaceTokens(string template, object values)
    {
        if (string.IsNullOrEmpty(template) || values == null)
            return template;

        var type = values.GetType();
        return TokenRegex().Replace(template, match =>
        {
            var propertyName = match.Groups[1].Value;
            var property = type.GetProperty(propertyName);
            return property?.GetValue(values)?.ToString() ?? match.Value;
        });
    }
}
