namespace Bit.Core.Settings;

public interface ILaunchDarklySettings
{
    public string SdkKey { get; set; }
    public string FlagDataFilePath { get; set; }
    public Dictionary<string, dynamic> FlagValues { get; set; }
}
