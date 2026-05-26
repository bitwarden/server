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
}
