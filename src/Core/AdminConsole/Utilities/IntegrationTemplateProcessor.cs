#nullable enable

using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bit.Core.AdminConsole.Utilities;

public static partial class IntegrationTemplateProcessor
{
    [GeneratedRegex(@"#(\w+)#")]
    private static partial Regex TokenRegex();

    public static string ReplaceTokens(string template, object values)
    {
        if (string.IsNullOrEmpty(template))
        {
            return template;
        }
        var type = values.GetType();
        return TokenRegex().Replace(template, match =>
        {
            var propertyName = match.Groups[1].Value;
            if (propertyName == "EventMessage")
            {
                return JsonSerializer.Serialize(values);
            }
            else
            {
                var property = type.GetProperty(propertyName);
                return property?.GetValue(values)?.ToString() ?? match.Value;
            }
        });
    }

    public static bool TemplateRequiresUser(string template)
    {
        if (string.IsNullOrEmpty(template))
        {
            return false;
        }

        return template.Contains("#UserName#", StringComparison.Ordinal)
               || template.Contains("#UserEmail#", StringComparison.Ordinal);
    }

    public static bool TemplateRequiresActingUser(string template)
    {
        if (string.IsNullOrEmpty(template))
        {
            return false;
        }

        return template.Contains("#ActingUserName#", StringComparison.Ordinal)
               || template.Contains("#ActingUserEmail#", StringComparison.Ordinal);
    }

    public static bool TemplateRequiresOrganization(string template)
    {
        if (string.IsNullOrEmpty(template))
        {
            return false;
        }

        return template.Contains("#OrganizationName#", StringComparison.Ordinal);
    }
}
