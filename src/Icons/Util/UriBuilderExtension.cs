#nullable enable

namespace Bit.Icons.Extensions;

public static class UriBuilderExtension
{
    public static bool TryBuild(this UriBuilder builder, out Uri? uri)
    {
        try
        {
            uri = builder.Uri;
            return true;
        }
        catch (UriFormatException)
        {
            uri = null;
            return false;
        }
    }
}
