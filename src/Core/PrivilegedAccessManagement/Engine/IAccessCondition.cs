namespace Bit.Core.PrivilegedAccessManagement.Engine;

public interface IAccessCondition
{
    AccessDecision Evaluate(AccessRuleEngineContext context);
}
