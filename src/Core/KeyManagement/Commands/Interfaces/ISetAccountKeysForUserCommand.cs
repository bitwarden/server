using Bit.Core.KeyManagement.Models.Api.Request;

namespace Bit.Core.KeyManagement.Commands.Interfaces;

public interface ISetAccountKeysForUserCommand
{
    Task SetAccountKeysForUserAsync(Guid userId,
        AccountKeysRequestModel accountKeys);
}
