namespace Bit.Core.Pam.Engine.Conditions;

public sealed class HumanApprovalCondition : IAccessCondition
{
    public AccessDecision Evaluate(AccessRuleEngineContext context)
    {
        return context.Rule.RequireApproval
            ? AccessDecision.RequiresApproval
            : AccessDecision.Allow;
    }
}
