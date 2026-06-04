namespace Bit.Core.Pam.Engine;

public sealed class AccessRuleEngineContext
{
    public required AccessRule Rule { get; init; }
    public required AccessRuleSignals Signals { get; init; }
}
