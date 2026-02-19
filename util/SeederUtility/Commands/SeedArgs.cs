using CommandDotNet;

namespace Bit.SeederUtility.Commands;

/// <summary>
/// CLI argument model for the seed command.
/// Supports loading presets from embedded resources.
/// </summary>
public class SeedArgs : IArgumentModel
{
    [Option("preset", Description = "Name of embedded preset to load")]
    public string? Preset { get; set; }

    [Option('l', "list", Description = "List all available presets and fixtures")]
    public bool List { get; set; }

    [Option("mangle", Description = "Enable mangling for test isolation")]
    public bool Mangle { get; set; }

    [Option("password", Description = "Password for all seeded accounts (default: asdfasdfasdf)")]
    public string? Password { get; set; }

    public void Validate()
    {
        if (List)
        {
            return;
        }

        if (string.IsNullOrEmpty(Preset))
        {
            throw new ArgumentException("--preset must be specified. Use --list to see available presets.");
        }
    }
}
