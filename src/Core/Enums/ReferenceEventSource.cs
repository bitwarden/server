using System.Runtime.Serialization;

namespace Bit.Core.Enums;

public enum ReferenceEventSource
{
    [EnumMember(Value = "organization")]
    Organization,
    [EnumMember(Value = "user")]
    User,
}
