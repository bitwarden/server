// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Models.Api;

namespace Bit.Api.AdminConsole.Models.Response.Organizations;

public class VerifiedOrganizationDomainSsoDetailsResponseModel(
    IEnumerable<VerifiedOrganizationDomainSsoDetailResponseModel> data,
    string continuationToken = null)
    : ListResponseModel<VerifiedOrganizationDomainSsoDetailResponseModel>(data, continuationToken);
