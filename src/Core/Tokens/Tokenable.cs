// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Text.Json;

namespace Bit.Core.Tokens;

public abstract class Tokenable
{
    public abstract bool Valid { get; }

    public Token ToToken()
    {
        return new Token(JsonSerializer.Serialize(this, this.GetType()));
    }

    public static T FromToken<T>(string token) => FromToken<T>(new Token(token));
    public static T FromToken<T>(Token token)
    {
        return JsonSerializer.Deserialize<T>(token.ToString());
    }
}
