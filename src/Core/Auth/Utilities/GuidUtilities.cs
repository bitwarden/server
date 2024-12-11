namespace Bit.Core.Auth.Utilities;

public static class GuidUtilities
{
    public static bool TryParseBytes(ReadOnlySpan<byte> bytes, out Guid guid)
    {
        try
        {
            guid = new Guid(bytes);
            return true;
        }
        catch
        {
            guid = Guid.Empty;
            return false;
        }
    }
}
