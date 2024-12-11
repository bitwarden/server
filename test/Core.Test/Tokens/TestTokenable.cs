using System.Text.Json.Serialization;
using Bit.Core.Tokens;

namespace Bit.Core.Test.Tokens;

public class TestTokenable : Tokenable
{
    public bool ForceInvalid { get; set; } = false;

    [JsonIgnore]
    public override bool Valid => !ForceInvalid;
}

public class TestExpiringTokenable : ExpiringTokenable
{
    private bool _forceInvalid;

    public TestExpiringTokenable()
        : this(false) { }

    public TestExpiringTokenable(bool forceInvalid)
    {
        _forceInvalid = forceInvalid;
    }

    protected override bool TokenIsValid() => !_forceInvalid;
}
