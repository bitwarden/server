namespace Bit.Core.Settings;

public interface ILaunchDarklySettings
{
    public string SdkKey { get; set; }
    public string FlagDataFilePath { get; set; }
    public Dictionary<string, string> FlagValues { get; set; }
}
