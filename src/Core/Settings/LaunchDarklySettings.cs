namespace Bit.Core.Settings;

public class LaunchDarklySettings : ILaunchDarklySettings
{
    public string SdkKey { get; set; }
    public string FlagDataFilePath { get; set; } = "flags.json";
    public Dictionary<string, string> FlagValues { get; set; } = new Dictionary<string, string>();
}
