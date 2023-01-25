namespace Bit.Core.SecretManagerFeatures.AccessPolicies.Interfaces;

public interface IDeleteAccessPolicyCommand
{
    Task DeleteAsync(Guid id, Guid userId);
}
