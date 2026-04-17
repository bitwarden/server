using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Models.Api;

namespace Bit.Api.KeyManagement.Models.Responses;

public class KeyConnectorConfirmationDetailsResponseModel : ResponseModel
{
    private const string _objectName = "keyConnectorConfirmationDetails";

    public KeyConnectorConfirmationDetailsResponseModel(KeyConnectorConfirmationDetails details,
        string obj = _objectName) : base(obj)
    {
        ArgumentNullException.ThrowIfNull(details);

        OrganizationName = details.OrganizationName;
    }

    public KeyConnectorConfirmationDetailsResponseModel() : base(_objectName)
    {
        OrganizationName = string.Empty;
    }

    public string OrganizationName { get; set; }
}
