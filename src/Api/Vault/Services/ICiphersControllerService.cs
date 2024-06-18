using System.Security.Claims;
using Bit.Api.Vault.Models.Response;

namespace Api.Vault.Services;

public interface ICiphersControllerService
{
    Task<IEnumerable<CipherMiniDetailsResponseModel>> GetOrganizationCiphers(ClaimsPrincipal user, Guid organizationId);
}
