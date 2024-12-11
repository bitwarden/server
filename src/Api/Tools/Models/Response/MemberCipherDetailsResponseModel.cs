using Bit.Core.Tools.Models.Data;

namespace Bit.Api.Tools.Models.Response;

public class MemberCipherDetailsResponseModel
{
    public string UserName { get; set; }
    public string Email { get; set; }
    public bool UsesKeyConnector { get; set; }

    /// <summary>
    /// A distinct list of the cipher ids associated with
    /// the organization member
    /// </summary>
    public IEnumerable<string> CipherIds { get; set; }

    public MemberCipherDetailsResponseModel(MemberAccessCipherDetails memberAccessCipherDetails)
    {
        this.UserName = memberAccessCipherDetails.UserName;
        this.Email = memberAccessCipherDetails.Email;
        this.UsesKeyConnector = memberAccessCipherDetails.UsesKeyConnector;
        this.CipherIds = memberAccessCipherDetails.CipherIds;
    }
}
