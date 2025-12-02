using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Core.KeyManagement.Repositories;
using Bit.Core.Repositories;

namespace Bit.Core.KeyManagement.Commands.Interfaces;

public interface ISetAccountKeysForUserCommand
{
    Task SetAccountKeysForUserAsync(Guid userId,
        AccountKeysRequestModel accountKeys,
        IUserRepository userRepository,
        IUserSignatureKeyPairRepository userSignatureKeyPairRepository);
}
