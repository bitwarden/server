using Bit.Core.Entities;
using Bit.Core.KeyManagement.Models.Api.Request;

namespace Bit.Core.KeyManagement.Commands.Interfaces;

public interface ISetKeyConnectorKeyCommand
{
    Task SetKeyConnectorKeyForUserAsync(User user, SetKeyConnectorKeyRequestModel requestModel);
}
