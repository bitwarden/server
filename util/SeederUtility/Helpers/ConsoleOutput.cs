using Bit.SeederUtility.Configuration;

namespace Bit.SeederUtility.Helpers;

internal static class ConsoleOutput
{
    private const int _labelWidth = 14;

    internal static void PrintRow(string label, object? value)
    {
        Console.WriteLine($"  {label,_labelWidth} : {value}");
    }

    internal static void PrintCountRow(string label, int count)
    {
        if (count > 0)
        {
            PrintRow(label, count);
        }
    }

    internal static void PrintMangleMap(SeederServiceScope deps)
    {
        if (!deps.Mangler.IsEnabled)
        {
            return;
        }

        var map = deps.Mangler.GetMangleMap();
        Console.Error.WriteLine($"--- Mangled Data Map ({map.Count} entries) ---");
        foreach (var (original, mangled) in map.Take(15))
        {
            Console.Error.WriteLine($"  {original} -> {mangled}");
        }

        if (map.Count > 15)
        {
            Console.Error.WriteLine($"  ... and {map.Count - 15} more");
        }
    }

    internal static void PrintSsoWiring(Guid organizationId, string identifier, string? ownerEmail)
    {
        var sp = $"http://localhost:51822/saml2/{organizationId}";
        Console.Error.WriteLine();
        Console.Error.WriteLine("--- SSO wiring (cloud Sso profile :51822) ---");
        Console.Error.WriteLine($"  Login identifier : {identifier}");
        Console.Error.WriteLine("  The org GUID is generated per seed. Wire it up one of two ways:");
        Console.Error.WriteLine();
        Console.Error.WriteLine("  ▸ Aspire (AppHost) — set the sso-org-id parameter to this org GUID, then start/restart");
        Console.Error.WriteLine("    the idp resource from the dashboard. No dev/.env edit needed.");
        Console.Error.WriteLine($"      sso-org-id: {organizationId}");
        Console.Error.WriteLine();
        Console.Error.WriteLine("  ▸ docker compose — paste these into dev/.env, then restart the IdP:  docker compose --profile idp up -d");
        Console.Error.WriteLine($"      IDP_SP_ENTITY_ID={sp}");
        Console.Error.WriteLine($"      IDP_SP_ACS_URL={sp}/Acs");

        if (ownerEmail is not null)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine("  Log in to SSO as this member. The local IdP identifies users via dev/authsources.php,");
            Console.Error.WriteLine("  not the database — add or update the matching login entry there (email AND uid together):");
            Console.Error.WriteLine("    '<username>:<password>' => array(");
            Console.Error.WriteLine($"        'email' => '{EscapePhpSingleQuotedString(ownerEmail)}',");
            Console.Error.WriteLine("        'uid'   => array('<unique-id>'),");
            Console.Error.WriteLine("    ),");
            Console.Error.WriteLine("  A no-mangle seed matches the default entry in dev/authsources.php.example. Live-mounted — no IdP restart needed.");
        }
    }

    /// <summary>
    /// Escapes backslashes and single quotes so the value stays valid inside a PHP single-quoted
    /// string literal when printed as a copy/paste snippet for dev/authsources.php.
    /// </summary>
    private static string EscapePhpSingleQuotedString(string value) =>
        value.Replace("\\", "\\\\").Replace("'", "\\'");
}
