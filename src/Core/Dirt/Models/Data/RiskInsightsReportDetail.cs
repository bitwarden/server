// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Core.Dirt.Reports.Models.Data;

public class RiskInsightsReportDetail
{
    public Guid? UserGuid { get; set; }
    public string UserName { get; set; }
    public string Email { get; set; }
    public bool UsesKeyConnector { get; set; }
    public IEnumerable<string> CipherIds { get; set; }
}
