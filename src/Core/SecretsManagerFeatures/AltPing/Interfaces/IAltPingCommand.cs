namespace Bit.Core.SecretsManagerFeatures.AltPing.Interfaces;

public interface IAltPingCommand
{
    Task<string> Ping();
}
