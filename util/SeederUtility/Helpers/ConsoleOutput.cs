using Bit.SeederUtility.Configuration;

namespace Bit.SeederUtility.Helpers;

internal static class ConsoleOutput
{
    internal static void PrintMangleMap(SeederServiceScope deps)
    {
        if (!deps.Mangler.IsEnabled)
        {
            return;
        }

        var map = deps.Mangler.GetMangleMap();
        Console.WriteLine($"--- Mangled Data Map ({map.Count} entries) ---");
        foreach (var (original, mangled) in map.Take(15))
        {
            Console.WriteLine($"  {original} -> {mangled}");
        }

        if (map.Count > 15)
        {
            Console.WriteLine($"  ... and {map.Count - 15} more");
        }
    }
}
