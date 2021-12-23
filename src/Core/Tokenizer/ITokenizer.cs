namespace Bit.Core.Tokenizer
{
    public interface ITokenizer<T> where T : ITokenable
    {
        string Protect(string key, T data);
        T Unprotect(string key, string token);
        bool TryUnprotect(string key, string token, out T data);
        bool TokenValid(string key, string token);
    }
}
