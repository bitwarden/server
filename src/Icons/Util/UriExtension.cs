
#nullable enable

using IdentityServer4.Extensions;

namespace Bit.Icons.Extensions;

public static class UriExtension
{
    public static bool IsHypertext(this Uri uri)
    {
        return uri.Scheme == "http" || uri.Scheme == "https";
    }

    public static Uri ChangeScheme(this Uri uri, string scheme)
    {
        return new UriBuilder(scheme, uri.Host) { Path = uri.PathAndQuery }.Uri;
    }

    public static Uri ChangeHost(this Uri uri, string host)
    {
        return new UriBuilder(uri) { Host = host }.Uri;
    }

    public static Uri ConcatPath(this Uri uri, params string[] paths)
        => uri.ConcatPath(paths.AsEnumerable());
    public static Uri ConcatPath(this Uri uri, IEnumerable<string> paths)
    {
        if (paths.IsNullOrEmpty())
        {
            return uri;
        }

        if (Uri.TryCreate(uri, paths.First(), out var newUri))
        {
            return newUri.ConcatPath(paths.Skip(1));
        }
        else
        {
            return uri;
        }
    }
}
