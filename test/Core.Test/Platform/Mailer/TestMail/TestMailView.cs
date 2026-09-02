using Bit.Core.Platform.Mail.Mailer;

namespace Bit.Core.Test.Platform.Mailer.TestMail;

public class TestMailView : BaseMailView
{
    public required string Name { get; init; }
}

public class TestMail : BaseMail<TestMailView>
{
    public override string Subject { get; set; } = "Test Email";
}
