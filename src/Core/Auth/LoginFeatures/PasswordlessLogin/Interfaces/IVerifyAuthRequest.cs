namespace Bit.Core.Auth.LoginFeatures.PasswordlessLogin.Interfaces;

public interface IVerifyAuthRequestCommand
{
    Task<bool> VerifyAuthRequestAsync(Guid authRequestId, string accessCode);
}
