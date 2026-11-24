using Bit.Core.Platform.Mail.Delivery;

namespace Bit.MailDelivery.Test;

public class SmtpOptionsTests
{
    [Fact]
    public void AuthType_Default_IsPassword()
    {
        var sut = new MailDeliveryOptions.SmtpOptions();

        Assert.Equal(SmtpAuthType.Password, sut.AuthType);
    }
}
