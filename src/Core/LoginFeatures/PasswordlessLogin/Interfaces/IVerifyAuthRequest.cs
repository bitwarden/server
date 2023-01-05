namespace Bit.Core.LoginFeatures.PasswordlessLogin.Interfaces;

public interface IVerifyAuthRequestCommand
{
    Task<bool> VerifyAuthRequestAsync(Guid authRequestId, string accessCode);
}
