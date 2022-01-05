namespace Bit.Core.Tokens
{
    public interface ITokenable
    {
        bool Valid { get; }
        Token ToToken();
    }
}
