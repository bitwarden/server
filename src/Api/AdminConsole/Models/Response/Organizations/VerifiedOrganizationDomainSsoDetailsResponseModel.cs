using Bit.Api.Models.Response;

namespace Bit.Api.AdminConsole.Models.Response.Organizations;

public class VerifiedOrganizationDomainSsoDetailsResponseModel(
    IEnumerable<VerifiedOrganizationDomainSsoDetailResponseModel> data,
    string continuationToken = null
) : ListResponseModel<VerifiedOrganizationDomainSsoDetailResponseModel>(data, continuationToken);
