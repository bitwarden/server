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
