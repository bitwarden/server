#nullable enable

using Bit.Icons.Models;

namespace Bit.Icons.Services;

public interface IUriService
{
    bool TryGetUri(string stringUri, out IconUri? iconUri);
    bool TryGetUri(Uri uri, out IconUri? iconUri);
    bool TryGetRedirect(HttpResponseMessage response, IconUri originalUri, out IconUri? iconUri);
}
