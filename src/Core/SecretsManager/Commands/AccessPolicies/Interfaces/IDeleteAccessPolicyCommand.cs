namespace Bit.Core.SecretsManager.Commands.AccessPolicies.Interfaces;

public interface IDeleteAccessPolicyCommand
{
    Task DeleteAsync(Guid id);
}
