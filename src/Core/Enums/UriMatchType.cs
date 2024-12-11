namespace Bit.Core.Enums;

public enum UriMatchType : byte
{
    Domain = 0,
    Host = 1,
    StartsWith = 2,
    Exact = 3,
    RegularExpression = 4,
    Never = 5,
}
