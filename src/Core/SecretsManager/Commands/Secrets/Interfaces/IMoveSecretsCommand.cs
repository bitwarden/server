using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Commands.Secrets.Interfaces;

public interface IMoveSecretsCommand
{
    Task MoveSecretsAsync(IEnumerable<Secret> secrets, Guid project);
}
