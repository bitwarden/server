using Bit.Core.Platform.Mailer;

namespace Bit.Core.Test.Platform.Services.TestMail;

public class TestMailView : BaseMailView
{
    public required string Name { get; init; }
}

public class TestMail : BaseMail<TestMailView>
{
    public override string Subject { get; } = "Test Email";
}
