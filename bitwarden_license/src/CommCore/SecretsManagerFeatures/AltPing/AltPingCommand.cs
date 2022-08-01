using Bit.Core.SecretsManagerFeatures.AltPing.Interfaces;

namespace Bit.CommCore.SecretsManagerFeatures.AltPing;

public class AltPingCommand: IAltPingCommand
{
    public Task<string> Ping() => Task.FromResult("pong");
}
