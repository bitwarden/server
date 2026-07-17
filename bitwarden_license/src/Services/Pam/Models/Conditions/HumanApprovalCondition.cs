using Bit.Services.Pam.Engine;

namespace Bit.Services.Pam.Models.Conditions;

/// <summary>
/// Always requires a human decision before a lease can be issued.
/// </summary>
/// <remarks>Wire format: <c>{ "kind": "human_approval" }</c></remarks>
public sealed class HumanApprovalCondition : AccessCondition
{
    public override AccessEvaluation Evaluate(AccessSignals signals) => AccessEvaluation.RequiresApproval;

    public override T Accept<T>(IAccessConditionVisitor<T> visitor) => visitor.VisitHumanApproval(this);
}
