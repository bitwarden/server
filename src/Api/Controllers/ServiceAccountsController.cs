using Bit.Api.Models.Response;
using Bit.Api.SecretManagerFeatures.Models.Response;
using Bit.Api.Utilities;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers;

[SecretsManager]
public class ServiceAccountsController : Controller
{
    private readonly IServiceAccountRepository _serviceAccountRepository;

    public ServiceAccountsController(IServiceAccountRepository serviceAccountRepository)
    {
        _serviceAccountRepository = serviceAccountRepository;
    }

    [HttpGet("organizations/{organizationId}/service-accounts")]
    public async Task<ListResponseModel<ServiceAccountResponseModel>> GetServiceAccountsByOrganizationAsync([FromRoute] Guid organizationId)
    {
        var serviceAccounts = await _serviceAccountRepository.GetManyByOrganizationIdAsync(organizationId);
        var responses = serviceAccounts.Select(serviceAccount => new ServiceAccountResponseModel(serviceAccount));
        return new ListResponseModel<ServiceAccountResponseModel>(responses);
    }
}
