namespace Bit.Core.SecretManagerFeatures.Secrets.Interfaces
{
    public interface IDeleteSecretCommand
    {
        Task<List<Tuple<Guid, string>>> DeleteSecrets(List<Guid> ids);
    }
}

