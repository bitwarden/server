using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.OrganizationFeatures.OrganizationApiKeys.Interfaces;

public interface IVerifyAuthRequestCommand
{
    Task<bool> VerifyAuthRequestAsync(Guid authRequestId, string accessCode);
}
