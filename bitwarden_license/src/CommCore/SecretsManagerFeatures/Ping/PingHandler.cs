using MediatR;

namespace Bit.CommCore.SecretsManagerFeatures.Ping;

public class PingHandler : IRequestHandler<Core.SecretsManagerFeatures.Ping.Ping, string>
{
    public Task<string> Handle(Core.SecretsManagerFeatures.Ping.Ping request, CancellationToken cancellationToken)
    {
        return Task.FromResult("Pong");
    }
}
