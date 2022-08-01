using Bit.Core.SecretsManagerFeatures.AltPing.Interfaces;

namespace Bit.Core.SecretsManagerFeatures.AltPing;

public class NoopAltPingCommand: IAltPingCommand
{
    public Task<string> Ping() => throw new NotImplementedException();
}
