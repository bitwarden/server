using Bit.Api.Models.Response;
using Bit.Api.SecretManagerFeatures.Models.Request;
using Bit.Api.SecretManagerFeatures.Models.Response;
using Bit.Api.Utilities;
using Bit.Core.Repositories;
using Bit.Core.SecretManagerFeatures.ServiceAccounts.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers;

[SecretsManager]
public class ServiceAccountsController : Controller
{
    private readonly IServiceAccountRepository _serviceAccountRepository;
    private readonly ICreateServiceAccountCommand _createServiceAccountCommand;
    private readonly IUpdateServiceAccountCommand _updateServiceAccountCommand;

    public ServiceAccountsController(IServiceAccountRepository serviceAccountRepository, ICreateServiceAccountCommand createServiceAccountCommand, IUpdateServiceAccountCommand updateServiceAccountCommand)
    {
        _serviceAccountRepository = serviceAccountRepository;
        _createServiceAccountCommand = createServiceAccountCommand;
        _updateServiceAccountCommand = updateServiceAccountCommand;
    }

    [HttpGet("organizations/{organizationId}/service-accounts")]
    public async Task<ListResponseModel<ServiceAccountResponseModel>> GetServiceAccountsByOrganizationAsync([FromRoute] Guid organizationId)
    {
        var serviceAccounts = await _serviceAccountRepository.GetManyByOrganizationIdAsync(organizationId);
        var responses = serviceAccounts.Select(serviceAccount => new ServiceAccountResponseModel(serviceAccount));
        return new ListResponseModel<ServiceAccountResponseModel>(responses);
    }


    [HttpPost("organizations/{organizationId}/service-accounts")]
    public async Task<ServiceAccountResponseModel> CreateServiceAccountAsync([FromRoute] Guid organizationId, [FromBody] ServiceAccountCreateRequestModel createRequest)
    {
        var result = await _createServiceAccountCommand.CreateAsync(createRequest.ToServiceAccount(organizationId));
        return new ServiceAccountResponseModel(result);
    }

    [HttpPut("service-accounts/{id}")]
    public async Task<ServiceAccountResponseModel> UpdateServiceAccountAsync([FromRoute] Guid id, [FromBody] ServiceAccountUpdateRequestModel updateRequest)
    {
        var result = await _updateServiceAccountCommand.UpdateAsync(updateRequest.ToServiceAccount(id));
        return new ServiceAccountResponseModel(result);
    }
}
