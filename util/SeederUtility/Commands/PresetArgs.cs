using CommandDotNet;

namespace Bit.SeederUtility.Commands;

/// <summary>
/// CLI argument model for the preset command.
/// Supports loading presets from embedded resources.
/// </summary>
public class PresetArgs : IArgumentModel
{
    [Option("name", Description = "Name of embedded preset to load")]
    public string? Name { get; set; }

    [Option('l', "list", Description = "List all available presets and fixtures")]
    public bool List { get; set; }

    [Option("output", Description = "Output format for --list: text or json (default: text)")]
    public string? Output { get; set; }

    public OutputFormat GetOutputFormat() =>
        string.IsNullOrWhiteSpace(Output)
            ? OutputFormat.Text
            : Enum.Parse<OutputFormat>(Output, ignoreCase: true);

    [Option("mangle", Description = "Enable mangling for test isolation")]
    public bool Mangle { get; set; }

    [Option("password", Description = "Password for all seeded accounts (default: asdfasdfasdf)")]
    public string? Password { get; set; }

    [Option("org-name", Description = "Override the organization display name from the preset/fixture")]
    public string? OrgName { get; set; }

    [Option("owner-email", Description = "Override the organization owner email (default: owner@<preset-domain>). Must not already exist in the User table; add --mangle to make repeat runs unique.")]
    public string? OwnerEmail { get; set; }

    [Option("kdf-iterations", Description = "KDF iteration count for all seeded users. Overrides the preset value if specified. Use 600000 for production-realistic e2e testing.")]
    public int? KdfIterations { get; set; }

    public void Validate()
    {
        if (!string.IsNullOrWhiteSpace(Output)
            && !Enum.TryParse<OutputFormat>(Output, ignoreCase: true, out _))
        {
            throw new ArgumentException($"Unrecognized output format '{Output}'. Allowed: text, json.");
        }

        if (List)
        {
            return;
        }

        if (string.IsNullOrEmpty(Name))
        {
            throw new ArgumentException("--name must be specified. Use --list to see available presets.");
        }

        if (KdfIterations.HasValue && KdfIterations.Value < 5_000)
        {
            throw new ArgumentException("KDF iterations must be at least 5,000.");
        }

        if (!string.IsNullOrWhiteSpace(OwnerEmail) && !OwnerEmail.Contains('@'))
        {
            throw new ArgumentException("--owner-email must be a valid email address (must contain '@').");
        }
    }
}
