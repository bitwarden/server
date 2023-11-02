using System.Diagnostics;
using Bit.Core.Settings;

namespace Bit.SharedWeb.Health;

public class DiagnosticsConfig
{
    public string ServiceName { get; }
    public ActivitySource ActivitySource { get; }

    public static DiagnosticsConfig For(GlobalSettings globalSettings)
    {
        return new DiagnosticsConfig(globalSettings);
    }

    private DiagnosticsConfig(GlobalSettings globalSettings)
    {
        ServiceName = globalSettings.ProjectName ?? "Bitwarden";
        ActivitySource = new ActivitySource(ServiceName);
    }
}
