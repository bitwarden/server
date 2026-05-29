using Bit.Core.KeyManagement.Models.Data;

namespace Bit.Core.KeyManagement.Queries.Interfaces;

public interface IKeyConnectorConfirmationDetailsQuery
{
    public Task<KeyConnectorConfirmationDetails> Run(string orgSsoIdentifier, Guid userId);
}
