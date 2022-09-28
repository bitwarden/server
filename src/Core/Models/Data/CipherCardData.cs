namespace Bit.Core.Models.Data;

public class CipherCardData : CipherData
{
    public CipherCardData() { }

    public string CardholderName { get; set; }
    public string Brand { get; set; }
    public string Number { get; set; }
    public string ExpMonth { get; set; }
    public string ExpYear { get; set; }
    public string Code { get; set; }
}
