using Bit.Core.Tokens;

namespace Bit.Test.Common.Fakes;

/// <summary>
/// Used to fake the IDataProtectorTokenFactory for testing purposes.
/// Generalized for use with all Tokenables.
/// </summary>
public class FakeDataProtectorTokenFactory<T> : IDataProtectorTokenFactory<T>
    where T : Tokenable, new()
{
    // Instead of real encryption, use a simple Dictionary to emulate protection/unprotection
    private readonly Dictionary<string, T> _tokenDatabase = new Dictionary<string, T>();

    public string Protect(T data)
    {
        // Generate a simple token representation
        var token = Guid.NewGuid().ToString();

        // Store the data against the token
        _tokenDatabase[token] = data;

        return token;
    }

    public T Unprotect(string token)
    {
        // If the token exists in the dictionary, return the corresponding data
        if (_tokenDatabase.TryGetValue(token, out var data))
        {
            return data;
        }

        // If the token doesn't exist, throw an exception similar to a decryption failure.
        throw new Exception("Failed to unprotect token.");
    }

    public bool TryUnprotect(string token, out T data)
    {
        try
        {
            data = Unprotect(token);
            return true;
        }
        catch
        {
            data = default;
            return false;
        }
    }

    public bool TokenValid(string token)
    {
        return _tokenDatabase.ContainsKey(token);
    }
}
