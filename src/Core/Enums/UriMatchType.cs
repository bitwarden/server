namespace Bit.Core.Enums
{
    public enum UriMatchType : byte
    {
        BaseDomain = 0,
        FullHostname = 1,
        StartsWith = 2,
        Exact = 3,
        RegularExpression = 4,
        Never = 5
    }
}
