namespace Bit.Core.Dirt.Models.Data;

public class RiskInsightsReportDetail
{
    public Guid? UserGuid { get; set; }
    public string UserName { get; set; }
    public string Email { get; set; }
    public bool UsesKeyConnector { get; set; }
    public IEnumerable<string> CipherIds { get; set; }
}
