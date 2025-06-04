namespace Bit.Core.Platform.Services;

#nullable enable


public class Mailer : IMailer
{
    public void SendEmail(BaseMailModel2 message, string recipient) => throw new NotImplementedException();

    public void SendEmails(BaseMailModel2 message, string[] recipients) => throw new NotImplementedException();
}
