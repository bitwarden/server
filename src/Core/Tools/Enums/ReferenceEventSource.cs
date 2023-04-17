using System.Runtime.Serialization;

namespace Bit.Core.Tools.Enums;

public enum ReferenceEventSource
{
    [EnumMember(Value = "organization")]
    Organization,
    [EnumMember(Value = "user")]
    User,
}
