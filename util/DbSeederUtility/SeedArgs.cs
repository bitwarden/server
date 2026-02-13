using CommandDotNet;

namespace Bit.DbSeederUtility;

/// <summary>
/// CLI argument model for the seed command.
/// Supports loading presets from embedded resources.
/// </summary>
public class SeedArgs : IArgumentModel
{
    [Option('p', "preset", Description = "Name of embedded preset to load (e.g., 'dunder-mifflin-full')")]
    public string? Preset { get; set; }

    [Option('l', "list", Description = "List all available presets and fixtures")]
    public bool List { get; set; }

    [Option("mangle", Description = "Enable mangling for test isolation")]
    public bool Mangle { get; set; }

    public void Validate()
    {
        // List mode is standalone
        if (List)
        {
            return;
        }

        // Must specify preset
        if (string.IsNullOrEmpty(Preset))
        {
            throw new ArgumentException(
                "--preset must be specified. Use --list to see available presets.");
        }
    }
}
