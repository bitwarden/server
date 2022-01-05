namespace Bit.Core.Tokens
{
    public interface IDataProtectorTokenFactory<T> where T : ITokenable
    {
        string Protect(T data);
        T Unprotect(string token);
        bool TryUnprotect(string token, out T data);
        bool TokenValid(string token);
    }
}
