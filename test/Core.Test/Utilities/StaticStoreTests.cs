using Bit.Core.Utilities;
using Xunit;

namespace Bit.Core.Test.Utilities;


public class StaticStoreTests
{
    [Fact]
    public void StaticStore_GlobalEquivalentDomains_OnlyAsciiAllowed()
    {
        // Ref: https://daniel.haxx.se/blog/2025/05/16/detecting-malicious-unicode/
        // URLs can contain unicode characters that to a computer would point to completely seperate domains but to the
        // naked eye look completely identical. For example 'g' and 'ց' look incredibly similar but when included in a
        // URL would lead you somewhere different. There is an opening for an attacker to contribute to Bitwarden with a
        // url update that could be missed in code review and then if they got a user to that URL Bitwarden could
        // consider it equivalent with a cipher in the users vault and offer autofill when we should not.
        // GitHub does now show a warning on non-ascii characters but it could still be missed.
        // https://github.blog/changelog/2025-05-01-github-now-provides-a-warning-about-hidden-unicode-text/

        // To defend against this:
        // Loop through all equivalent domains and fail if any contain a non-ascii character
        // non-ascii character can make a valid URL so it's possible that in the future we have a domain
        // we want to allow list, that should be done through `continue`ing in the below foreach loop
        // only if the domain strictly equals (do NOT use InvariantCulture comparison) the one added to our allow list.
        foreach (var domain in StaticStore.GlobalDomains.SelectMany(p => p.Value))
        {
            for (var i = 0; i < domain.Length; i++)
            {
                var character = domain[i];
                Assert.True(char.IsAscii(character), $"Domain: {domain} contains non-ASCII character: '{character}' at index: {i}");
            }
        }
    }
}
