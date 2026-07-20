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

    internal static void PrintSsoWiring(Guid organizationId, string identifier, string? ownerEmailOverride)
    {
        var sp = $"http://localhost:51822/saml2/{organizationId}";
        Console.Error.WriteLine();
        Console.Error.WriteLine("--- SSO wiring (cloud Sso profile :51822) ---");
        Console.Error.WriteLine($"  Login identifier : {identifier}");
        Console.Error.WriteLine("  Add to dev/.env, then restart the IdP:  docker compose --profile idp up -d");
        Console.Error.WriteLine($"    IDP_SP_ENTITY_ID={sp}");
        Console.Error.WriteLine($"    IDP_SP_ACS_URL={sp}/Acs");

        if (ownerEmailOverride is not null)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine("  --owner-email was set. The local IdP identifies you via dev/authsources.php, not");
            Console.Error.WriteLine("  the database — add or update your login entry there (email AND uid together):");
            Console.Error.WriteLine("    '<username>:<password>' => array(");
            Console.Error.WriteLine($"        'email' => '{ownerEmailOverride}',");
            Console.Error.WriteLine("        'uid'   => array('<unique-id>'),");
            Console.Error.WriteLine("    ),");
            Console.Error.WriteLine("  See dev/authsources.php.example for the default (no-override) entry. Live-mounted — no IdP restart needed.");
        }
    }
}
