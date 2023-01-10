using Bit.Core.LoginFeatures.PasswordlessLogin.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Utilities;

namespace Bit.Core.LoginFeatures.PasswordlessLogin;

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
        if (authRequest == null || !CoreHelpers.FixedTimeEquals(authRequest.AccessCode, accessCode))
        {
            return false;
        }
        return true;
    }
}
