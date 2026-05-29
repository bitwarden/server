namespace Bit.Core.PrivilegedAccessManagement.Engine;

public sealed class AccessRuleEngineContext
{
    public required AccessRule Rule { get; init; }
    public required AccessRuleSignals Signals { get; init; }
}
