using Bitwarden.Server.Sdk.Environment;

namespace Bit.GlobalSettingsBridge.Test;

internal sealed class TestBitwardenEnvironment : IBitwardenEnvironment
{
    public string Version => "";
    public string? GitHash => null;
    public required bool SelfHosted { get; init; }
    public required string? SelfHostFlavor { get; init; }
}
