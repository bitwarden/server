using System.Text.RegularExpressions;

namespace Bit.Seeder.Migration.Utils;

public static class SecuritySanitizer
{
    private static readonly string[] SensitiveFields =
    [
        "password",
        "passwd",
        "pwd",
        "secret",
        "key",
        "token",
        "api_key",
        "auth_token",
        "access_token",
        "private_key"
    ];

    public static string MaskPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
            return string.Empty;

        if (password.Length <= 4)
            return "***";

        return password[..2] + new string('*', password.Length - 4) + password[^2..];
    }

    public static Dictionary<string, object> SanitizeConfigForDisplay(Dictionary<string, object> configDict)
    {
        var sanitized = new Dictionary<string, object>();

        foreach (var (key, value) in configDict)
        {
            // Use case-insensitive comparison with proper culture handling
            if (SensitiveFields.Any(field => field.Equals(key, StringComparison.OrdinalIgnoreCase)))
            {
                sanitized[key] = value != null ? MaskPassword(value.ToString() ?? string.Empty) : string.Empty;
            }
            else if (value is Dictionary<string, object> nestedDict)
            {
                sanitized[key] = SanitizeConfigForDisplay(nestedDict);
            }
            else
            {
                sanitized[key] = value;
            }
        }

        return sanitized;
    }

    public static string SanitizeLogMessage(string message)
    {
        var patterns = new Dictionary<string, string>
        {
            [@"password\s*[:=]\s*['""]?([^'""\s,}]+)['""]?"] = "password=***",
            [@"passwd\s*[:=]\s*['""]?([^'""\s,}]+)['""]?"] = "passwd=***",
            [@"""password""\s*:\s*""[^""]*"""] = @"""password"": ""***""",
            [@"'password'\s*:\s*'[^']*'"] = @"'password': '***'"
        };

        var sanitized = message;
        foreach (var (pattern, replacement) in patterns)
        {
            sanitized = Regex.Replace(sanitized, pattern, replacement, RegexOptions.IgnoreCase);
        }

        return sanitized;
    }

    public static string CreateSafeConnectionString(string host, int port, string database, string username)
    {
        return $"{username}@{host}:{port}/{database}";
    }
}
