using Bit.Core.Dirt.Reports.Models.Data;
namespace Bit.Api.Dirt.Models.Response;

public class MemberCipherDetailsResponseModel
{
    public Guid? UserGuid { get; set; }
    public string UserName { get; set; }
    public string Email { get; set; }
    public bool UsesKeyConnector { get; set; }

    /// <summary>
    /// A distinct list of the cipher ids associated with
    /// the organization member
    /// </summary>
    public IEnumerable<string> CipherIds { get; set; }

    public MemberCipherDetailsResponseModel(RiskInsightsReportDetail reportDetail)
    {
        this.UserGuid = reportDetail.UserGuid;
        this.UserName = reportDetail.UserName;
        this.Email = reportDetail.Email;
        this.UsesKeyConnector = reportDetail.UsesKeyConnector;
        this.CipherIds = reportDetail.CipherIds;
    }
}
