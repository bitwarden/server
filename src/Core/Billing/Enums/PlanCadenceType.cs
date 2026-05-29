using System.Runtime.Serialization;

namespace Bit.Core.Billing.Enums;

public enum PlanCadenceType
{
    [EnumMember(Value = "annually")]
    Annually,
    [EnumMember(Value = "monthly")]
    Monthly
}
