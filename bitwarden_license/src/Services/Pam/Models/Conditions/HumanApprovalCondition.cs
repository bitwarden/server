using System.Text.Json.Serialization;
using Bit.Pam.Enums;
using Bit.Services.Pam.Engine;

namespace Bit.Services.Pam.Models.Conditions;

/// <summary>Always requires a human decision before a lease can be issued.</summary>
/// <remarks>Wire format: <c>{ "kind": "human_approval" }</c></remarks>
public sealed class HumanApprovalCondition : AccessCondition
{
    [JsonIgnore]
    public override AccessConditionKind Kind => AccessConditionKind.HumanApproval;

    public override AccessEvaluation Evaluate(AccessSignals signals) => AccessEvaluation.RequiresApproval;

    public override AccessRuleValidationResult Validate() => AccessRuleValidationResult.Valid;
}
