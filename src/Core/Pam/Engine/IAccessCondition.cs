namespace Bit.Core.Pam.Engine;

public interface IAccessCondition
{
    AccessDecision Evaluate(AccessRuleEngineContext context);
}
