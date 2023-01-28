namespace Bit.Core.Models.Business;

public class ExpiringToken
{
    public readonly string Token;
    public readonly DateTime ExpirationDate;

    public ExpiringToken(string token, DateTime expirationDate)
    {
        Token = token;
        ExpirationDate = expirationDate;
    }
}
