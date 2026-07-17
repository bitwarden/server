namespace Bit.Services.Pam.Models.Conditions;

/// <summary>
/// A type-safe operation over the closed set of <see cref="AccessCondition"/> kinds, dispatched via
/// <see cref="AccessCondition.Accept{T}"/>. Implemented once per operation — the engine evaluates a condition, the
/// validator checks it is well-formed — with <typeparamref name="T"/> as that operation's result. Because the
/// interface has one method per kind, the compiler forces every implementation to handle every kind, so a newly
/// added condition can never be silently skipped by evaluation or validation.
/// </summary>
public interface IAccessConditionVisitor<out T>
{
    T VisitHumanApproval(HumanApprovalCondition condition);
    T VisitIpAllowlist(IpAllowlistCondition condition);
    T VisitTimeOfDay(TimeOfDayCondition condition);
}
