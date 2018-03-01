namespace Bit.Core.Enums
{
    public enum UriMatchType : byte
    {
        BaseDomain = 0,
        FullHostname = 1,
        FullUri = 2,
        StartsWith = 3,
        RegularExpression = 4
    }
}
