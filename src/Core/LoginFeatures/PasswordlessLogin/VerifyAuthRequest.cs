using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.OrganizationFeatures.OrganizationApiKeys.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Utilities;

namespace Bit.Core.OrganizationFeatures.OrganizationApiKeys;

public class VerifyAuthRequestCommand : IVerifyAuthRequestCommand
{
    private readonly IAuthRequestRepository _authRequestRepository;

    public VerifyAuthRequestCommand(IAuthRequestRepository authRequestRepository)
    {
        _authRequestRepository = authRequestRepository;
    }

    public async Task<bool> VerifyAuthRequestAsync(Guid authRequestId, string accessCode)
    {
        var authRequest = await _authRequestRepository.GetByIdAsync(authRequestId);
        if (authRequest == null || authRequest.AccessCode != accessCode) 
        {
            return false;
        }
        return true;
    }
}
