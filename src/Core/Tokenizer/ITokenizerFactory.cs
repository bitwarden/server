namespace Bit.Core.Tokenizer
{
    public interface ITokenizerFactory
    {
        ITokenizer<T> Create<T>(string clearTextPrefix, TokenType targetTokenType) where T : ITokenable;
    }
}
